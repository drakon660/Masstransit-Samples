# Masstransit-Samples

MassTransit sample applications demonstrating sagas, routing slips, request/response, scheduling, and message data (claim-check) on RabbitMQ or Azure Service Bus.

The active sample lives under [`Sample-Twitch/`](Sample-Twitch) and targets **.NET 10** with **MassTransit 8.5**.

## What's in the box

Two services that coordinate over the bus to model an order workflow:

```
Client.Console ──HTTP──▶ Sample.Api ──bus──▶ Sample.Service ──bus──▶ Warehouse.Service
                                              (Order saga)            (Allocation saga)
```

| Project | Role |
| --- | --- |
| `Sample.Api` | ASP.NET Core HTTP API. Submits/accepts orders, queries status. |
| `Sample.Service` | Hosts `OrderStateMachine`, order consumers, and Courier activities. |
| `Warehouse.Service` | Hosts `AllocationStateMachine` and inventory consumer. |
| `Sample.Components` / `Warehouse.Components` | Consumers, sagas, and Routing Slip activities. |
| `Sample.Contracts` / `Warehouse.Contracts` | Message contracts shared across the bus. |
| `Client.Console` | Interactive load generator that POSTs orders and polls status. |
| `Sample.Components.Tests` | NUnit specs using MassTransit's test harness. |

## Prerequisites

- .NET 10 SDK
- Docker (for the infrastructure stack)

## Running the sample

```bash
# 1. Start infrastructure (RabbitMQ, MongoDB, Redis, SQL Server, Quartz, Seq, Jaeger)
docker compose -f docker-compose.infra.yml up -d

# 2. Build everything
cd Sample-Twitch
dotnet build Sample-Twitch.slnx

# 3. Run the services (each in its own terminal)
dotnet run --project Sample.Service
dotnet run --project Warehouse.Service
dotnet run --project Sample.Api          # http://localhost:5000

# 4. Drive load
dotnet run --project Client.Console
```

The API exposes Swagger UI at `http://localhost:5000/swagger`. Ad-hoc requests live in [`Sample.Api/run.http`](Sample-Twitch/Sample.Api/run.http).

## Tests

```bash
cd Sample-Twitch
dotnet test Sample.Components.Tests/Sample.Components.Tests.csproj
```

## Infrastructure

`docker-compose.infra.yml` brings up everything the services connect to:

| Service | Endpoint | Notes |
| --- | --- | --- |
| RabbitMQ | `amqp://iaai:iaai@localhost:5672` | Management UI on `15672` |
| MongoDB | `mongodb://127.0.0.1:27018` | Saga persistence + claim-check storage |
| Redis | `localhost:6379` | |
| SQL Server | `localhost:1433` | Quartz scheduler backing store |
| Quartz | (via RabbitMQ `queue:quartz`) | Used by `AllocationStateMachine` for hold expiration |
| Seq | `http://localhost:8082` (ingest `5341`) | Serilog sink |
| Jaeger | `http://localhost:16686` | OTLP receiver on `4317`/`4318` |

### Switching transport

Transport selection is gated by a `Microsoft.FeatureManagement` feature flag named `UseAzureServiceBus`. Default is `false` → RabbitMQ. Set it to `true` and provide `ConnectionStrings:AzureServiceBus` to flip all hosts to Azure Service Bus:

```json
{
  "FeatureManagement": {
    "UseAzureServiceBus": true
  },
  "ConnectionStrings": {
    "AzureServiceBus": "Endpoint=sb://..."
  }
}
```

You can also override at runtime via env var (`FeatureManagement__UseAzureServiceBus=true`) or command line (`--FeatureManagement:UseAzureServiceBus=true`).

## Order flow

1. `Client.Console` POSTs an order to `Sample.Api`.
2. `OrderController2.Create` issues `IRequestClient<SubmitOrder>` → `SubmitOrderConsumer` accepts (or rejects `"TEST"` customers) and publishes `OrderSubmitted`.
3. `OrderStateMachine` correlates by `OrderId` and transitions to `Submitted`. The client then PATCHes `/order/{id}/accept` → `OrderAccepted` → `Accepted`.
4. `FulfillOrderConsumer` runs fulfillment; faults move the saga to `Faulted`, where `ChangeCardNumber` can resubmit.
5. `Warehouse.Service` runs an independent `AllocationStateMachine` that schedules a hold expiration via Quartz.
6. The client polls `GET /order/{id}/status`, served via `IRequestClient<CheckOrder>` against the saga (with an `OnMissingInstance` responder returning `OrderNotFound`).
