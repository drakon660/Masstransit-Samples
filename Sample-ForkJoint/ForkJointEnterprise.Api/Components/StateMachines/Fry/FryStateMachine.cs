namespace ForkJointEnterprise.Api.Components.StateMachines;

public class FryStateMachine :
    MassTransitStateMachine<FryState>
{
    public FryStateMachine()
    {
        Event(() => FriesOrdered, x => x.CorrelateById(context => context.Message.OrderLineId));
        Event(() => FriesCompleted, x => x.CorrelateById(context => context.Message.OrderLineId));
        Event(() => FriesFaulted, x => x.CorrelateById(context => context.Message.Message.OrderLineId));

        InstanceState(x => x.CurrentState, WaitingForCompletion, Completed, Faulted);

        Initially(
            When(FriesOrdered)
                .InitializeFuture()
                .Then(context =>
                {
                    context.Saga.OrderId = context.Message.OrderId;
                    context.Saga.Size = context.Message.Size;
                })
                .Activity(x => x.OfInstanceType<CookFryActivity>())
                .TransitionTo(WaitingForCompletion)
        );

        During(WaitingForCompletion,
            When(FriesOrdered)
                .PendingRequestStarted()
        );

        During(Completed,
            When(FriesOrdered)
                .RespondAsync(x => x.CreateFriesCompleted())
        );

        During(Faulted,
            When(FriesOrdered)
                .RespondAsync(x => x.CreateFriesFaulted())
        );

        DuringAny(
            When(FriesCompleted)
                .SetCompleted(x => x.CreateFriesCompleted())
                .TransitionTo(Completed),
            When(FriesFaulted)
                .FaultFries()
                .SetFaulted(x => x.CreateFriesFaulted())
                .TransitionTo(Faulted)
        );
    }

    public State WaitingForCompletion { get; } = null!;
    public State Completed { get; } = null!;
    public State Faulted { get; } = null!;

    public Event<OrderFry> FriesOrdered { get; } = null!;
    public Event<FryReady> FriesCompleted { get; } = null!;
    public Event<Fault<CookFry>> FriesFaulted { get; } = null!;
}


public static class FriesStateMachineExtensions
{
    public static EventActivityBinder<FryState, Fault<CookFry>> FaultFries(this EventActivityBinder<FryState,
        Fault<CookFry>> binder)
    {
        return binder.Then(context => context.Saga.ExceptionInfo = context.Message.Exceptions.FirstOrDefault()!);
    }

    public static async Task<FryCompleted> CreateFriesCompleted<T>(this BehaviorContext<FryState, T> context)
        where T : class
    {
        var tuple = await context.Init<FryCompleted>(new
        {
            context.Saga.Created,
            context.Saga.Completed,
            context.Saga.OrderId,
            OrderLineId = context.Saga.CorrelationId,
            context.Saga.Size,
            Description = $"{context.Saga.Size} Fries"
        });
        return tuple.Message;
    }

    public static async Task<FryFaulted> CreateFriesFaulted<T>(this BehaviorContext<FryState, T> context)
        where T : class
    {
        var tuple = await context.Init<FryFaulted>(new
        {
            context.Saga.Created,
            context.Saga.Faulted,
            context.Saga.OrderId,
            OrderLineId = context.Saga.CorrelationId,
            Description = $"{context.Saga.Size} Fries",
            context.Saga.ExceptionInfo
        });
        return tuple.Message;
    }
}
