using MassTransit.Courier.Contracts;

namespace ForkJoint.Api.Components.StateMachines;

public class BurgerStateMachine :
    MassTransitStateMachine<BurgerState>
{
    public BurgerStateMachine(ILogger<BurgerStateMachine> logger)
    {
        Event(() => BurgerRequested, x => x.CorrelateById(context => context.Message.Burger.BurgerId));
        Event(() => BurgerCompleted, x => x.CorrelateById(instance => instance.TrackingNumber, context => context.Message.TrackingNumber));
        Event(() => BurgerFaulted, x => x.CorrelateById(instance => instance.TrackingNumber, context => context.Message.TrackingNumber));

        InstanceState(x => x.CurrentState, WaitingForCompletion, Completed, Faulted);

        // .RequestStarted() / .RequestCompleted() bridge an asynchronous saga to a synchronous IRequestClient<T> caller.
        //
        // RequestStarted(): saga received a request via IRequestClient. Records inbound RequestId + ResponseAddress,
        // publishes a RequestStarted event consumed by MassTransit's built-in RequestStateMachine (registered as
        // RequestState saga). That creates a separate RequestState instance tracking the in-flight request.
        //
        // RequestCompleted(fn): saga finished its work. Publishes RequestCompleted carrying the response payload.
        // RequestStateMachine matches it back to the tracked RequestState, sends response to the original
        // ResponseAddress with the correct RequestId header. Client's IRequestClient resolves.
        //
        // Why: decouples response from saga lifecycle (no manual RespondAsync), survives saga restarts
        // (RequestState persisted independently), deduplicates concurrent inbound requests for same correlation,
        // and gives built-in deadletter/timeout handling for orphan requests.
        //
        // Requires RequestStateMachine + RequestState + RequestSagaDefinition registered in DI — without it,
        // RequestStarted/RequestCompleted events have nowhere to land and the client times out.
        //
        // Only meaningful when the inbound message has RequestId + ResponseAddress headers, which are set by
        // IRequestClient<T>. Plain Publish/Send messages have no RequestId, so RequestStarted records nothing
        // and RequestCompleted has no one to respond to — both silently skip (harmless, but no value).
        // If no caller uses request/response, drop these methods and skip the RequestStateMachine registration.
        Initially(
            When(BurgerRequested)
                .Then(context =>
                {
                    logger.LogInformation("BurgerStateMachine RequestId: {RequestId}", context.RequestId);

                    context.Saga.OrderId = context.Message.OrderId;
                    context.Saga.Burger = context.Message.Burger;
                })
                .Activity(x => x.OfInstanceType<PrepareBurgerActivity>())
                .RequestStarted()
                .TransitionTo(WaitingForCompletion));

        During(WaitingForCompletion,
            When(BurgerRequested)
                .Then(context =>
                {
                    logger.LogInformation("BurgerStateMachine RequestId: {RequestId} (duplicate request)", context.RequestId);
                })
                .RequestStarted(),
            When(BurgerCompleted)
                .Then(context => context.Saga.Burger = context.GetVariable<Burger>("Burger") ?? context.Saga.Burger)
                .RequestCompleted(CreateBurgerCompleted)
                .TransitionTo(Completed),
            When(BurgerFaulted)
                .Then(context =>
                {
                    context.Saga.Reason = context.Message.ActivityExceptions.Select(x => x.ExceptionInfo).FirstOrDefault()?.Message ?? "Unknown";
                })
                .RequestCompleted(CreateBurgerNotCompleted)
                .TransitionTo(Faulted));

        During(Completed,
            When(BurgerRequested)
                .RespondAsync(CreateBurgerCompleted));
        During(Faulted,
            When(BurgerRequested)
                .RespondAsync(CreateBurgerNotCompleted));
    }

    public State WaitingForCompletion { get; } = null!;
    public State Completed { get; } = null!;
    public State Faulted { get; } = null!;

    public Event<RequestBurger> BurgerRequested { get; } = null!;
    public Event<RoutingSlipCompleted> BurgerCompleted { get; } = null!;
    public Event<RoutingSlipFaulted> BurgerFaulted { get; } = null!;

    static async Task<BurgerCompleted> CreateBurgerCompleted<T>(BehaviorContext<BurgerState, T> context)
        where T : class
    {
        var tuple = await context.Init<BurgerCompleted>(new
        {
            context.Saga.OrderId,
            context.Saga.Burger
        });
        return tuple.Message;
    }

    static async Task<BurgerNotCompleted> CreateBurgerNotCompleted<T>(BehaviorContext<BurgerState, T> context)
        where T : class
    {
        var tuple = await context.Init<BurgerNotCompleted>(new
        {
            context.Saga.OrderId,
            context.Saga.Burger,
            context.Saga.Reason
        });
        return tuple.Message;
    }
}
