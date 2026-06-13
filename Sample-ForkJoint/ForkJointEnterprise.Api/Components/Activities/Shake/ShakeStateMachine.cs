namespace ForkJointEnterprise.Api.Components.StateMachines;

public class ShakeStateMachine :
    MassTransitStateMachine<ShakeState>
{
    public ShakeStateMachine()
    {
        Event(() => ShakeOrdered, x => x.CorrelateById(context => context.Message.OrderLineId));
        Event(() => ShakeCompleted, x => x.CorrelateById(context => context.Message.OrderLineId));
        Event(() => ShakeFaulted, x => x.CorrelateById(context => context.Message.Message.OrderLineId));

        InstanceState(x => x.CurrentState, WaitingForCompletion, Completed, Faulted);

        Initially(
            When(ShakeOrdered)
                .InitializeFuture()
                .Then(context =>
                {
                    context.Saga.OrderId = context.Message.OrderId;
                    context.Saga.Flavor = context.Message.Flavor;
                    context.Saga.Size = context.Message.Size;
                })
                .Activity(x => x.OfInstanceType<PourShakeActivity>())
                .TransitionTo(WaitingForCompletion)
        );

        During(WaitingForCompletion,
            When(ShakeOrdered)
                .PendingRequestStarted()
        );

        During(Completed,
            When(ShakeOrdered)
                .RespondAsync(x => x.CreateShakeCompleted())
        );

        During(Faulted,
            When(ShakeOrdered)
                .RespondAsync(x => x.CreateShakeFaulted())
        );

        DuringAny(
            When(ShakeCompleted)
                .SetCompleted(x => x.CreateShakeCompleted())
                .TransitionTo(Completed),
            When(ShakeFaulted)
                .FaultShake()
                .SetFaulted(x => x.CreateShakeFaulted())
                .TransitionTo(Faulted)
        );
    }

    public State WaitingForCompletion { get; } = null!;
    public State Completed { get; } = null!;
    public State Faulted { get; } = null!;

    public Event<OrderShake> ShakeOrdered { get; } = null!;
    public Event<ShakeReady> ShakeCompleted { get; } = null!;
    public Event<Fault<PourShake>> ShakeFaulted { get; } = null!;
}


public static class ShakeStateMachineExtensions
{
    public static EventActivityBinder<ShakeState, Fault<PourShake>> FaultShake(this EventActivityBinder<ShakeState,
        Fault<PourShake>> binder)
    {
        return binder.Then(context => context.Saga.ExceptionInfo = context.Message.Exceptions.FirstOrDefault()!);
    }

    public static async Task<ShakeCompleted> CreateShakeCompleted<T>(this BehaviorContext<ShakeState, T> context)
        where T : class
    {
        var tuple = await context.Init<ShakeCompleted>(new
        {
            context.Saga.Created,
            context.Saga.Completed,
            context.Saga.OrderId,
            OrderLineId = context.Saga.CorrelationId,
            context.Saga.Flavor,
            context.Saga.Size,
            Description = $"{context.Saga.Size} {context.Saga.Flavor} Shake"
        });
        return tuple.Message;
    }

    public static async Task<ShakeFaulted> CreateShakeFaulted<T>(this BehaviorContext<ShakeState, T> context)
        where T : class
    {
        var tuple = await context.Init<ShakeFaulted>(new
        {
            context.Saga.Created,
            context.Saga.Faulted,
            context.Saga.OrderId,
            OrderLineId = context.Saga.CorrelationId,
            Description = $"{context.Saga.Size} {context.Saga.Flavor} Shake",
            context.Saga.ExceptionInfo
        });
        return tuple.Message;
    }
}
