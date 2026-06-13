namespace ForkJointEnterprise.Api.Components.StateMachines;

public class OnionRingsStateMachine :
    MassTransitStateMachine<OnionRingsState>
{
    public OnionRingsStateMachine()
    {
        Event(() => OnionRingsOrdered, x => x.CorrelateById(context => context.Message.OrderLineId));
        Event(() => OnionRingsCompleted, x => x.CorrelateById(context => context.Message.OrderLineId));
        Event(() => OnionRingsFaulted, x => x.CorrelateById(context => context.Message.Message.OrderLineId));

        InstanceState(x => x.CurrentState, WaitingForCompletion, Completed, Faulted);

        Initially(
            When(OnionRingsOrdered)
                .InitializeFuture()
                .Then(context =>
                {
                    context.Saga.OrderId = context.Message.OrderId;
                    context.Saga.Quantity = context.Message.Quantity;
                })
                .Activity(x => x.OfInstanceType<CookOnionRingsActivity>())
                .TransitionTo(WaitingForCompletion)
        );

        During(WaitingForCompletion,
            When(OnionRingsOrdered)
                .PendingRequestStarted()
        );

        During(Completed,
            When(OnionRingsOrdered)
                .RespondAsync(x => x.CreateOnionRingsCompleted())
        );

        During(Faulted,
            When(OnionRingsOrdered)
                .RespondAsync(x => x.CreateOnionRingsFaulted())
        );

        DuringAny(
            When(OnionRingsCompleted)
                .SetCompleted(x => x.CreateOnionRingsCompleted())
                .TransitionTo(Completed),
            When(OnionRingsFaulted)
                .FaultOnionRings()
                .SetFaulted(x => x.CreateOnionRingsFaulted())
                .TransitionTo(Faulted)
        );
    }

    public State WaitingForCompletion { get; } = null!;
    public State Completed { get; } = null!;
    public State Faulted { get; } = null!;

    public Event<OrderOnionRings> OnionRingsOrdered { get; } = null!;
    public Event<OnionRingsReady> OnionRingsCompleted { get; } = null!;
    public Event<Fault<CookOnionRings>> OnionRingsFaulted { get; } = null!;
}


public static class OnionRingsStateMachineExtensions
{
    public static EventActivityBinder<OnionRingsState, Fault<CookOnionRings>> FaultOnionRings(this EventActivityBinder<OnionRingsState,
        Fault<CookOnionRings>> binder)
    {
        return binder.Then(context => context.Saga.ExceptionInfo = context.Message.Exceptions.FirstOrDefault()!);
    }

    public static async Task<OnionRingsCompleted> CreateOnionRingsCompleted<T>(this BehaviorContext<OnionRingsState, T> context)
        where T : class
    {
        var tuple = await context.Init<OnionRingsCompleted>(new
        {
            context.Saga.Created,
            context.Saga.Completed,
            context.Saga.OrderId,
            OrderLineId = context.Saga.CorrelationId,
            context.Saga.Quantity,
            Description = $"{context.Saga.Quantity} Onion Rings"
        });
        return tuple.Message;
    }

    public static async Task<OnionRingsFaulted> CreateOnionRingsFaulted<T>(this BehaviorContext<OnionRingsState, T> context)
        where T : class
    {
        var tuple = await context.Init<OnionRingsFaulted>(new
        {
            context.Saga.Created,
            context.Saga.Faulted,
            context.Saga.OrderId,
            OrderLineId = context.Saga.CorrelationId,
            Description = $"{context.Saga.Quantity} Onion Rings",
            context.Saga.ExceptionInfo
        });
        return tuple.Message;
    }
}
