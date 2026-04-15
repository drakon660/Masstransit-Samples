# In-Memory Outbox vs In-Memory Inbox+Outbox

Notes captured while debugging the `Instances of abstract classes cannot be created`
runtime error from `OutBoxConsumer`. The MassTransit docs don't cover this clearly,
so the conclusions here come from reading the source of
[`InMemoryOutboxConfigurationExtensions.cs`](https://github.com/MassTransit/MassTransit/blob/develop/src/MassTransit/Configuration/InMemoryOutboxConfigurationExtensions.cs)
and [discussion #5430](https://github.com/MassTransit/MassTransit/discussions/5430).

## TL;DR

| API | What it does | Needs `Add*` registration? | When to use |
| --- | --- | --- | --- |
| `UseInMemoryOutbox(context)` | Buffers `Publish`/`Send` calls until the consumer returns successfully. No dedup. | **No** | Production-safe. Use when you only need "don't publish if the consumer throws." |
| `UseInMemoryInboxOutbox(context)` | Outbox **plus** dedup of incoming messages by `MessageId`. | **Yes** — `services.AddInMemoryInboxOutbox()` | Test scenarios. The source comment literally says *"intended for testing scenarios."* For production dedup use the EF Core or MongoDB outbox. |

## The runtime trap

```csharp
public class OutBoxConsumerDefinition : ConsumerDefinition<OutBoxConsumer>
{
    protected override void ConfigureConsumer(
        IReceiveEndpointConfigurator endpointConfigurator,
        IConsumerConfigurator<OutBoxConsumer> consumerConfigurator,
        IRegistrationContext context)
    {
        endpointConfigurator.UseMessageRetry(r => r.Immediate(2));
        endpointConfigurator.UseInMemoryInboxOutbox(context); // throws at runtime
    }
}
```

The endpoint compiles fine, the bus starts fine, then on the first message you get:

```
System.InvalidOperationException: Instances of abstract classes cannot be created.
   at Microsoft.Extensions.DependencyInjection.ActivatorUtilities.CreateInstance(...)
   at MassTransit.DependencyInjection.CreatedConsumeScopeContext`1.GetService[T]()
   at MassTransit.Middleware.OutboxConsumeFilter`2.Send(...)
```

### Why

`UseInMemoryInboxOutbox` only wires the **filter**. The filter then tries to resolve
the inbox repository (and a couple of other services) from the consume scope's DI
container. Those services are never registered automatically — you have to register
them yourself with the sibling call:

```csharp
public static IServiceCollection AddInMemoryInboxOutbox(this IServiceCollection collection)
```

When DI can't find the registration, it falls through to `ActivatorUtilities.CreateInstance`,
which tries to construct the abstract repository interface and blows up.

This is the same pattern as the EF Core / Mongo outboxes:

```csharp
// Production: persistent inbox+outbox — both halves required
builder.Services.AddMassTransit(x =>
{
    x.AddEntityFrameworkOutbox<MyDbContext>(o => { ... });   // Add* — registers DI services
    // ...
});
// then on the receive endpoint:
endpointConfigurator.UseEntityFrameworkOutbox<MyDbContext>(context);   // Use* — wires filter
```

`UseInMemoryOutbox` is the **only** outbox API that "just works" without an `Add*`
companion, because the in-memory outbox-only path keeps buffered messages on the
`ConsumeContext` itself — there is no DI-resolved repository to construct.

## Correct setups

### A. You only need publish-buffering (recommended for the demo)

```csharp
// Program.cs — nothing extra needed
builder.Services.AddMassTransit(cfg => { /* ... */ });

// ConsumerDefinition
endpointConfigurator.UseMessageRetry(r => r.Immediate(2));
endpointConfigurator.UseInMemoryOutbox(context);
```

What this gives you:
- Failed consume attempt → buffered `Publish` is **discarded**.
- Retry succeeds → buffered `Publish` is flushed once.
- `OutBoxClientConsumer` sees the event exactly once per successful consume.

What it does **not** give you:
- Dedup if the broker redelivers the same `MessageId` after a crash/ack timeout.

### B. You want full inbox + outbox semantics

```csharp
// Program.cs — register the in-memory repositories first
builder.Services.AddInMemoryInboxOutbox();
builder.Services.AddMassTransit(cfg => { /* ... */ });

// ConsumerDefinition
endpointConfigurator.UseMessageRetry(r => r.Immediate(2));
endpointConfigurator.UseInMemoryInboxOutbox(context);
```

What this adds on top of A:
- Incoming `MessageId` is recorded in the in-memory inbox after a successful consume.
- A redelivery of the same `MessageId` is filtered out before reaching the consumer.
- Lives entirely in process memory, so it dies on host restart — fine for tests,
  not a substitute for the EF/Mongo outbox in production.

### C. Production dedup

Use one of the persistent outboxes — both halves of the inbox/outbox state are
durable across restarts:

```csharp
builder.Services.AddMassTransit(x =>
{
    x.AddEntityFrameworkOutbox<MyDbContext>(o =>
    {
        o.QueryDelay = TimeSpan.FromSeconds(1);
        o.UseSqlServer();
        o.UseBusOutbox();
    });
});
```

then `UseEntityFrameworkOutbox<MyDbContext>(context)` on the receive endpoint
(or via `AddConfigureEndpointsCallback` to apply it to every endpoint at once).

## Quick decision tree

- *"I just want to make sure failed consumes don't leak side-effect publishes."* → `UseInMemoryOutbox`
- *"I'm writing a unit test that needs to exercise inbox dedup without a database."* → `AddInMemoryInboxOutbox` + `UseInMemoryInboxOutbox`
- *"I'm running this in production and need dedup across restarts."* → `AddEntityFrameworkOutbox` (or `AddMongoDbOutbox`) + the matching `Use*`

## What the docs actually say

The official [in-memory outbox page](https://masstransit.io/documentation/patterns/in-memory-outbox)
only documents `UseInMemoryOutbox`. The combined `UseInMemoryInboxOutbox` is
public API but undocumented — its only marker is the source comment calling it
out for testing use.
