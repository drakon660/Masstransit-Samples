# ForkJoint.Api

Two HTTP endpoints, two orchestration patterns. Both end up driving the same set of `IExecuteActivity` / `IActivity` instances via a MassTransit **routing slip**, but they get there very differently.

| Endpoint | Pattern | Entry point | Orchestrator | Response bridge |
|----------|---------|-------------|--------------|-----------------|
| `POST /orders`  | Consumer + Routing Slip                       | `IRequestClient<SubmitOrder>`  | `SubmitOrderConsumer`             | `SubmitOrderResponseConsumer` (subscribed to slip events) |
| `POST /burgers` | Saga State Machine + Activity + Routing Slip  | `IRequestClient<RequestBurger>` | `BurgerStateMachine` saga         | `RequestStateMachine` (built-in, bridges saga ↔ request client) |

Underneath both flows: `GrillBurgerActivity` (compensating, full `IActivity<,>`) and `DressBurgerActivity` (execute-only, `IExecuteActivity<>`), wired together by `BurgerItineraryPlanner`.

---

## 1. `POST /orders` — consumer + routing slip

```
Client ──HTTP POST /orders──▶ SubmitOrderModule
                                │  IRequestClient<SubmitOrder>
                                ▼
                       submit-order queue
                                │
                                ▼
                       SubmitOrderConsumer
                       (RoutingSlipRequestConsumer<SubmitOrder>)
                                │
                       BuildItinerary:
                         - AddSubscription(InputAddress, Completed|Faulted)
                         - AddVariable(OrderId, Request, RequestId, ResponseAddress, FaultAddress, Deadline)
                         - IBurgerItineraryPlanner.PlanItinerary(burger, builder)
                                │
                                ▼
                       context.Execute(routingSlip)
                                │
                                ▼
       grill-burger queue ─▶ GrillBurgerActivity.Execute
                                │
                                ▼
       dress-burger queue ─▶ DressBurgerActivity.Execute
                                │
                  ┌─────────────┴─────────────┐
              success                       failure
                  │                             │
                  ▼                             ▼
       RoutingSlipCompleted          RoutingSlipFaulted
       (sent to submit-order queue via subscription)
                  │                             │
                  ▼                             ▼
       SubmitOrderResponseConsumer.Consume(RoutingSlipCompleted/Faulted)
        - Reads RequestId / ResponseAddress / Request from slip Variables
        - GetResponseEndpoint(responseAddress, requestId)
        - Sends OrderCompleted / OrderNotCompleted
                  │
                  ▼
       IRequestClient<SubmitOrder> resolves
       SubmitOrderModule returns 200 OK or 422 Problem
```

### Key files
- `Modules/SubmitOrderModule.cs` — Carter route, awaits `GetResponse<OrderCompleted, OrderNotCompleted>`.
- `Components/Consumers/SubmitOrderConsumer.cs` — extends `RoutingSlipRequestConsumer<SubmitOrder>`. Builds the slip; the base class is responsible for the `AddSubscription(...)` / variable plumbing.
- `Components/Consumers/SubmitOrderResponseConsumer.cs` — extends `RoutingSlipResponseConsumer<SubmitOrder, OrderCompleted, OrderNotCompleted>`. Materialises the response from slip variables.
- `Components/Activities/ItineraryPlanners/BurgerItineraryPlanner.cs` — adds grill + dress activities to the builder.

### Why no saga
The original `SubmitOrder` request is short-lived and process-bound. The consumer can stay alive long enough to await the slip and respond. No state needs to survive a restart, so a saga adds zero value.

---

## 2. `POST /burgers` — saga state machine + activity + routing slip

```
Client ──HTTP POST /burgers──▶ BurgerModule
                                │  IRequestClient<RequestBurger>
                                ▼
                       burger-state queue (saga endpoint, kebab-case of BurgerStateMachine)
                                │
                                ▼
                       BurgerStateMachine
                       Event: BurgerRequested (correlated by Burger.BurgerId)
                                │
                       Initially(When(BurgerRequested)
                            .Then(seed Saga.OrderId/Burger)
                            .Activity(OfInstanceType<PrepareBurgerActivity>)   ◀── DI-resolved activity
                            .RequestStarted()                                  ◀── publishes RequestStarted event
                            .TransitionTo(WaitingForCompletion))
                                │
                                ▼
                       PrepareBurgerActivity.Execute (on the saga instance)
                            - AddSubscription(InputAddress, Completed|Faulted)
                            - AddVariable(OrderId, BurgerId, Deadline?)
                            - IBurgerItineraryPlanner.PlanItinerary(burger, builder)
                            - context.Execute(routingSlip)
                            - Saga.TrackingNumber = trackingNumber
                                │
                                ▼
       grill-burger queue ─▶ GrillBurgerActivity
       dress-burger queue ─▶ DressBurgerActivity
                                │
                  ┌─────────────┴─────────────┐
              success                       failure
                  │                             │
                  ▼                             ▼
       RoutingSlipCompleted          RoutingSlipFaulted
       (delivered to the saga endpoint via subscription)
                  │                             │
       BurgerCompleted event         BurgerFaulted event
       (correlated by TrackingNumber)
                  │                             │
                  ▼                             ▼
       During(WaitingForCompletion,
         When(BurgerCompleted)          When(BurgerFaulted)
           .Then(pull Burger var)         .Then(record Reason)
           .RequestCompleted(             .RequestCompleted(
              CreateBurgerCompleted)         CreateBurgerNotCompleted)
           .TransitionTo(Completed))      .TransitionTo(Faulted))
                  │
                  ▼
       RequestStateMachine (built-in MassTransit.Components saga)
        - tracked the original RequestId/ResponseAddress (recorded by RequestStarted())
        - now matches the RequestCompleted event
        - sends the response back to the original responseAddress with the right RequestId
                  │
                  ▼
       IRequestClient<RequestBurger> resolves
       BurgerModule returns 200 OK (BurgerCompleted) or 422 Problem (BurgerNotCompleted)
```

### Key files
- `Modules/BurgerModule.cs` — Carter route, awaits `GetResponse<BurgerCompleted, BurgerNotCompleted>`.
- `Components/StateMachines/BurgerStateMachine.cs` — events, states, `Initially / During / RequestStarted / RequestCompleted` wiring.
- `Components/StateMachines/BurgerState.cs` — saga instance (`SagaStateMachineInstance`).
- `Components/StateMachines/PrepareBurgerActivity.cs` — `IStateMachineActivity<BurgerState>`. Builds the routing slip from inside a saga transition.
- `Components/StateMachines/BurgerSagaDefinition.cs` — endpoint-level config (concurrency, partitioning).
- `Components/StateMachines/RequestSagaDefinition.cs` — config for the built-in `RequestStateMachine` saga.

### Why a saga
- Correlation by `Burger.BurgerId`, not request lifetime. The saga survives across messages.
- Duplicate `RequestBurger` arrivals are deduped by correlation; second call just gets `.RequestStarted()` again.
- The slip's completion event arrives asynchronously — possibly after restart — and the saga still has the context (`TrackingNumber`, `OrderId`, `Burger`) to react.

### Why `RequestStarted()` / `RequestCompleted()` (and `RequestStateMachine`)
The saga is asynchronous, but the caller used `IRequestClient<T>` (synchronous-looking request/response). Something has to remember the inbound `RequestId` + `ResponseAddress` and send the response when the saga eventually finishes.

`.RequestStarted()` publishes a `RequestStarted` event consumed by the built-in `RequestStateMachine` (its own saga instance per request).
`.RequestCompleted(fn)` publishes the response payload. `RequestStateMachine` correlates it to the stored request and sends the actual reply with the correct `RequestId` header, so the original `IRequestClient` resolves.

Without `RequestStateMachine` registered, those events have no consumer → caller times out. That's why `Program.cs` registers both:

```csharp
x.AddSagaStateMachine<BurgerStateMachine, BurgerState, BurgerSagaDefinition>()
    .InMemoryRepository();
x.AddSagaStateMachine<MassTransit.Components.RequestStateMachine, MassTransit.Components.RequestState, RequestSagaDefinition>()
    .InMemoryRepository();
```

These methods are no-ops when the inbound message has no `RequestId` (i.e. plain `Publish` / `Send`). Useful only when the caller uses an `IRequestClient`.

---

## Shared building blocks

### Routing slip
A pre-built itinerary of activity addresses + arguments + variables. `RoutingSlipBuilder.AddSubscription(InputAddress, RoutingSlipEvents.Completed | Faulted)` makes the courier publish `RoutingSlipCompleted` / `RoutingSlipFaulted` events back to a chosen endpoint when the slip terminates — that's how both flows get their async completion signal.

### Variables vs. arguments
- **Arguments** are passed to a single activity at the moment it executes (from the slip's `Activity.Arguments`).
- **Variables** live on the slip for its whole lifetime and are accessible from every activity and from the completion event consumer. We use variables to ferry `OrderId`, `RequestId`, `ResponseAddress`, etc. across the slip → response boundary.

### Activity types
- `IActivity<TArguments, TLog>` — has compensation. `GrillBurgerActivity` returns a `GrillBurgerLog` so compensation can put the patty back into the warmer.
- `IExecuteActivity<TArguments>` — execute only, no compensation. `DressBurgerActivity` is execute-only.
- Important DI gotcha: `AddActivitiesFromNamespaceContaining<T>` only registers `IActivity<,>` (compensating). Execute-only activities must be added with `AddExecuteActivity<TActivity, TArguments>()`, or you get no endpoint and silent timeouts. That's why `Program.cs` has both:

```csharp
x.AddActivitiesFromNamespaceContaining<CourierActivities>();
x.AddExecuteActivity<DressBurgerActivity, DressBurgerArguments>();
```

### Endpoint naming
`x.SetKebabCaseEndpointNameFormatter()` + `cfg.ConfigureEndpoints(context)` auto-derive queue names from the type name. Anywhere the code constructs a queue URI by hand (e.g. inside `BurgerItineraryPlanner`), the literal must match what the formatter would produce.

---

## TL;DR

- `POST /orders` = "stateless workflow." Request-bound consumer drives a routing slip; a sibling consumer turns slip events back into a reply. No saga.
- `POST /burgers` = "stateful workflow per burger." A saga owns the lifecycle; a state-machine activity launches the slip; slip events feed the saga; `RequestStarted/RequestCompleted` + the built-in `RequestStateMachine` bridge the async saga to the synchronous request client.
- Same activities (`GrillBurgerActivity`, `DressBurgerActivity`) reused in both flows because they live on their own endpoints — the orchestration shape decides who calls them and who receives the result.
