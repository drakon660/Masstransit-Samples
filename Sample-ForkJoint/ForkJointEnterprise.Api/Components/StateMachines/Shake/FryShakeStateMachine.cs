namespace ForkJointEnterprise.Api.Components.StateMachines;

public class FryShakeStateMachine :
    MassTransitStateMachine<FryShakeState>
{
    public FryShakeStateMachine(ILogger<FryShakeStateMachine> logger)
    {
        Event(() => FryShakeOrdered, x => x.CorrelateById(context => context.Message.OrderLineId));
        Event(() => FryCompleted, x => x.CorrelateById(context => context.Message.OrderLineId));
        Event(() => FryFaulted, x => x.CorrelateById(context => context.Message.OrderLineId));
        Event(() => ShakeCompleted, x => x.CorrelateById(context => context.Message.OrderLineId));
        Event(() => ShakeFaulted, x => x.CorrelateById(context => context.Message.OrderLineId));

        InstanceState(x => x.CurrentState, WaitingForCompletion, Completed, Faulted);

        Initially(
            When(FryShakeOrdered)
                .LogFryShake(logger, "Ordered", "Initial")
                .InitializeFuture()
                .Then(context =>
                {
                    context.Saga.OrderId = context.Message.OrderId;
                    context.Saga.Size = context.Message.Size;
                })
                .Activity(x => x.OfType<OrderFryShakeActivity>())
                .LogFryShake(logger, "FanOut done", "-> WaitingForCompletion")
                .TransitionTo(WaitingForCompletion)
        );

        During(WaitingForCompletion,
            When(FryShakeOrdered)
                .LogFryShake(logger, "Duplicate request", "WaitingForCompletion")
                .PendingRequestStarted()
        );

        During(Completed,
            When(FryShakeOrdered)
                .LogFryShake(logger, "Replay (already completed)", "Completed")
                .RespondAsync(x => x.CreateFryShakeCompleted())
        );

        During(Faulted,
            When(FryShakeOrdered)
                .LogFryShake(logger, "Replay (already faulted)", "Faulted")
                .RespondAsync(x => x.CreateFryShakeFaulted())
        );

        DuringAny(
            When(FryFaulted)
                .LogFryShakeFaulted(logger, "Fry faulted")
                .FaultFry()
                .SetFaulted(x => x.CreateFryShakeFaulted())
                .TransitionTo(Faulted)
        );

        DuringAny(
            When(ShakeFaulted)
                .LogFryShakeFaulted(logger, "Shake faulted")
                .FaultShake()
                .SetFaulted(x => x.CreateFryShakeFaulted())
                .TransitionTo(Faulted)
        );

        DuringAny(
            When(BothCompleted)
                .LogFryShakeComposite(logger, "Both completed -> Completed")
                .SetCompleted(x => x.CreateFryShakeCompleted())
                .TransitionTo(Completed)
        );

        CompositeEvent(() => BothCompleted, x => x.BothState, FryCompleted, ShakeCompleted);
    }

    public State WaitingForCompletion { get; } = null!;
    public State Completed { get; } = null!;
    public State Faulted { get; } = null!;

    public Event BothCompleted { get; } = null!;

    public Event<OrderFryShake> FryShakeOrdered { get; } = null!;
    public Event<FryCompleted> FryCompleted { get; } = null!;
    public Event<FryFaulted> FryFaulted { get; } = null!;
    public Event<ShakeCompleted> ShakeCompleted { get; } = null!;
    public Event<ShakeFaulted> ShakeFaulted { get; } = null!;
}


public static class FryShakeStateMachineExtensions
{
    public static EventActivityBinder<FryShakeState, T> LogFryShake<T>(
        this EventActivityBinder<FryShakeState, T> binder, ILogger logger, string action, string state)
        where T : class
    {
        return binder.Then(context =>
            logger.LogInformation("FryShake [{Action}] OrderId={OrderId} LineId={LineId} Size={Size} Flavor={Flavor} State={State} BothState={BothState}",
                action, context.Saga.OrderId, context.Saga.CorrelationId, context.Saga.Size, context.Saga.Flavor, state, context.Saga.BothState));
    }

    public static EventActivityBinder<FryShakeState, T> LogFryShakeFaulted<T>(
        this EventActivityBinder<FryShakeState, T> binder, ILogger logger, string action)
        where T : class
    {
        return binder.Then(context =>
            logger.LogWarning("FryShake [{Action}] OrderId={OrderId} LineId={LineId} State={State}",
                action, context.Saga.OrderId, context.Saga.CorrelationId, context.Saga.CurrentState));
    }

    public static EventActivityBinder<FryShakeState> LogFryShakeComposite(
        this EventActivityBinder<FryShakeState> binder, ILogger logger, string action)
    {
        return binder.Then(context =>
            logger.LogInformation("FryShake [{Action}] OrderId={OrderId} LineId={LineId} Size={Size} Flavor={Flavor} BothState={BothState}",
                action, context.Saga.OrderId, context.Saga.CorrelationId, context.Saga.Size, context.Saga.Flavor, context.Saga.BothState));
    }

    public static EventActivityBinder<FryShakeState, FryFaulted> FaultFry(this EventActivityBinder<FryShakeState, FryFaulted> binder)
    {
        return binder.Then(context => context.Saga.ExceptionInfo = context.Message.ExceptionInfo);
    }

    public static EventActivityBinder<FryShakeState, ShakeFaulted> FaultShake(this EventActivityBinder<FryShakeState, ShakeFaulted> binder)
    {
        return binder.Then(context => context.Saga.ExceptionInfo = context.Message.ExceptionInfo);
    }

    public static async Task<FryShakeCompleted> CreateFryShakeCompleted<T>(this BehaviorContext<FryShakeState, T> context)
        where T : class
    {
        var tuple = await context.Init<FryShakeCompleted>(new
        {
            context.Saga.Created,
            context.Saga.Completed,
            context.Saga.OrderId,
            OrderLineId = context.Saga.CorrelationId,
            context.Saga.Size,
            context.Saga.Flavor,
            Description = $"{context.Saga.Size} {context.Saga.Flavor} Fry Shake"
        });
        return tuple.Message;
    }

    public static async Task<FryShakeCompleted> CreateFryShakeCompleted(this BehaviorContext<FryShakeState> context)
    {
        var tuple = await context.Init<FryShakeCompleted>(new
        {
            context.Saga.Created,
            context.Saga.Completed,
            context.Saga.OrderId,
            OrderLineId = context.Saga.CorrelationId,
            context.Saga.Size,
            context.Saga.Flavor,
            Description = $"{context.Saga.Size} {context.Saga.Flavor} Fry Shake"
        });
        return tuple.Message;
    }

    public static async Task<FryShakeFaulted> CreateFryShakeFaulted<T>(this BehaviorContext<FryShakeState, T> context)
        where T : class
    {
        var tuple = await context.Init<FryShakeFaulted>(new
        {
            context.Saga.Created,
            context.Saga.Faulted,
            context.Saga.OrderId,
            OrderLineId = context.Saga.CorrelationId,
            Description = $"{context.Saga.Size} {context.Saga.Flavor} Fry Shake",
            context.Saga.ExceptionInfo
        });
        return tuple.Message;
    }
}
