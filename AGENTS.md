# AGENTS.md

This file provides guidance to Codex (Codex.ai/code) when working with code in this repository.

## Repository layout

This repo contains MassTransit sample applications. The active sample lives under `Sample-Twitch/` and uses the new `.slnx` solution format (`Sample-Twitch/Sample-Twitch.slnx`). All projects target **.NET 10** and use **MassTransit 8.5.x**.

## Common commands

Run all commands from `Sample-Twitch/` unless noted.

```bash
# Start required infrastructure (RabbitMQ, MongoDB, Redis, SQL Server, Quartz, Seq, Zipkin)
docker compose -f ../docker-compose.infra.yml up -d

# Build / restore the whole solution
dotnet build Sample-Twitch.slnx

# Run a service (each is its own host)
dotnet run --project Sample.Api          # HTTP API on http://localhost:5000
dotnet run --project Sample.Service      # Order saga + consumers
dotnet run --project Warehouse.Service   # Allocation saga + inventory consumer
dotnet run --project Client.Console      # Interactive load generator

# Tests (NUnit)
dotnet test Sample.Components.Tests/Sample.Components.Tests.csproj

# Run a single test by name
dotnet test Sample.Components.Tests/Sample.Components.Tests.csproj --filter "FullyQualifiedName~OrderStateMachine_Specs"
```

The API exposes Swagger UI; ad-hoc requests live in `Sample.Api/run.http`.

## Infrastructure expectations

`docker-compose.infra.yml` (at repo root) is the source of truth for what the services connect to. Notable bindings used by the code:

- RabbitMQ: `amqp://iaai:iaai@localhost:5672` (management UI on `15672`) — set in `Sample.Api/appsettings.json` and `Sample.Service/appsettings.json`.
- MongoDB: `mongodb://127.0.0.1:27018` (host port is **27018**, not the default 27017). Saga repositories and the `MongoDbMessageDataRepository` (used for large-payload claim-check) point here. Databases: `orders` (Sample.Service), `allocations` (Warehouse.Service), `attachments` (message data).
- Quartz scheduler: scheduled via `queue:quartz` (configured by `UseMessageScheduler`). The `quartz` container itself talks to the `sqlserver` container — both must be running for any `Schedule(...)`/`UseMessageScheduler` code paths.
- Seq: `http://localhost:5341` (Serilog sink). Web UI on `8082`.
- Jaeger: OTLP receiver on `localhost:4317` (gRPC, default for `AddOtlpExporter`) and `4318` (HTTP). Web UI on `http://localhost:16686`. All services export traces here, source `MassTransit`.

Switching transport: each service evaluates the `UseAzureServiceBus` Microsoft.FeatureManagement flag at startup. Set `FeatureManagement:UseAzureServiceBus` to `true` (in `appsettings.json`, env var `FeatureManagement__UseAzureServiceBus=true`, or command line) to flip all hosts to `UsingAzureServiceBus` (which then reads `ConnectionStrings:AzureServiceBus`). Default is RabbitMQ.

## Architectural overview

The sample models an order workflow split across two bounded contexts that communicate only through MassTransit messages. There is **no shared database** between services — they coordinate via the bus.

```
Client.Console ──HTTP──▶ Sample.Api ──bus──▶ Sample.Service ──bus──▶ Warehouse.Service
                                              (Order saga)            (Allocation saga)
```

### Project roles

- **Sample.Contracts / Warehouse.Contracts** — message contracts only (interfaces/POCOs). These are the bus boundary; both producer and consumer reference them. Keep them free of behavior.
- **Sample.Infrastructure** — tiny shared library used by all three hosts. Wraps `Microsoft.FeatureManagement` for the transport toggle. Defines `FeatureFlags` (string constants for flag names) and `IConfiguration.IsFeatureEnabledAsync(...)`, an extension that builds a one-shot service provider so hosts can evaluate flags **before** `builder.Build()` (i.e. before the host's own service provider exists) without tripping the ASP0000 analyzer. Hosts also call `builder.Services.AddFeatureManagement()` so that consumers/controllers can inject `IFeatureManager` later.
- **Sample.Components** — order-side handlers: `Consumers/`, `BatchConsumers/`, `CourierActivities/` (Routing Slip activities), and `StateMachines/OrderStateMachine.cs`. Has no transport configuration.
- **Warehouse.Components** — warehouse-side handlers and `AllocationStateMachine`.
- **Sample.Service / Warehouse.Service** — composition roots. Each is a `Host.CreateApplicationBuilder` console host that wires `AddMassTransit`, picks the transport, and registers consumers/sagas via `AddConsumersFromNamespaceContaining<...>`/`AddSagaStateMachine<...>`.
- **Sample.Api** — ASP.NET Core host. Issues `IRequestClient<SubmitOrder>` (wired to a specific queue via `KebabCaseEndpointNameFormatter`) and `IRequestClient<CheckOrder>` (resolved via routing), and publishes events like `OrderAccepted`, `ChangeCardNumber`. Active controller is `OrderController2.cs`; `OrderController.cs` is fully commented out (kept for reference).
- **Sample.Components.Tests** — NUnit specs using MassTransit's test harness (`OrderStateMachine_Specs`, `SubmitOrderConsumer_Specs`).

### Cross-cutting conventions

- **Endpoint naming**: every host registers `KebabCaseEndpointNameFormatter.Instance` as a singleton, then calls `ConfigureEndpoints(context)` so queue names are auto-derived from consumer/saga type names. When code constructs queue URIs by hand (e.g. `new Uri("queue:submit-order")` in `OrderStateMachine` and `OrderController2.ResubmitOrder`), the literal must match the kebab-cased consumer name.
- **Saga persistence**: both state machines use MongoDB repositories (`MongoDbRepository`). There is commented-out Redis wiring in `Sample.Service/Program.cs` left as an example — note the comment "redis will not work because redisrepository doesn't support queries" in `OrderStateMachine.cs`, which is why the `AccountClosed` event (correlated by `CustomerNumber`, not `CorrelationId`) requires Mongo.
- **Large messages**: both Sample.Api and Sample.Service configure `UseMessageData(new MongoDbMessageDataRepository(...))` to offload large payloads (e.g. the `Notes` field the console client pads to several KB) to Mongo via the claim-check pattern.
- **Scheduling**: `Warehouse.Service` uses `Schedule(() => HoldExpiration, ...)` in the allocation saga, which requires `UseMessageScheduler(new Uri("queue:quartz"))` and a running Quartz service.
- **Observability**: every host installs Serilog → Seq and OpenTelemetry → OTLP (Jaeger) with `tracing.AddSource("MassTransit")`. When adding a new host, mirror this pattern so traces stay correlated across the bus.
- **Telemetry**: all hosts call `cfg.DisableUsageTelemetry()`.

### Order flow (read this before touching the saga)

1. `Client.Console` POSTs an `OrderModel` to `Sample.Api`.
2. `OrderController2.Create` uses `IRequestClient<SubmitOrder>` to request/respond against `SubmitOrderConsumer` (rejected if customer number contains `"TEST"`); on success the consumer publishes `OrderSubmitted`.
3. `OrderStateMachine` (in `Sample.Service`) correlates by `OrderId`, transitions `Initial → Submitted`. The client then PATCHes `/order/{id}/accept`, which publishes `OrderAccepted` → saga runs `AcceptOrderActivity` and transitions to `Accepted`.
4. `FulfillOrderConsumer` handles fulfillment; failures (`Fault<FulfillOrder>` or explicit `OrderFulfillmentFaulted`) move the saga to `Faulted`. `ChangeCardNumber` while `Faulted` re-sends `FulfillOrder` and returns to `Accepted`.
5. `Warehouse.Service` runs an independent `AllocationStateMachine` that schedules a `HoldExpiration` via Quartz and finalizes when the hold expires or a release is requested.
6. The console client polls `GET /order/{id}/status`, which the API serves via `IRequestClient<CheckOrder>` → `OrderStatusRequested` event on the saga (with an `OnMissingInstance` responder that returns `OrderNotFound`).

When editing the saga, remember the `DuringAny` block at the bottom defines the `OrderStatusRequested` responder for every state — new states automatically inherit it.
