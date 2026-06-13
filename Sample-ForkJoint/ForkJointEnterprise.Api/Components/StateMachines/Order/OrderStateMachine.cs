namespace ForkJointEnterprise.Api.Components.StateMachines;

public class OrderStateMachine :
    MassTransitStateMachine<OrderState>
{
    public OrderStateMachine(ILogger<OrderStateMachine> logger)
    {
        Event(() => OrderSubmitted, x => x.CorrelateById(context => context.Message.OrderId));
        Event(() => LineCompleted, x => x.CorrelateById(context => context.Message.OrderId));
        Event(() => LineFaulted, x => x.CorrelateById(context => context.Message.OrderId));

        InstanceState(x => x.CurrentState, WaitingForCompletion, Completed, Faulted);

        Initially(
            When(OrderSubmitted)
                .LogSubmitted(logger)
                .InitializeFuture()
                .Then(context =>
                {
                    context.Saga.LineCount = 0;

                    if (context.Message.Burgers != null)
                    {
                        context.Saga.LineCount += context.Message.Burgers.Length;
                        context.Saga.LinesPending.UnionWith(context.Message.Burgers.Select(x => x.BurgerId));
                    }

                    if (context.Message.Fries != null)
                    {
                        context.Saga.LineCount += context.Message.Fries.Length;
                        context.Saga.LinesPending.UnionWith(context.Message.Fries.Select(x => x.FryId));
                    }

                    if (context.Message.Shakes != null)
                    {
                        context.Saga.LineCount += context.Message.Shakes.Length;
                        context.Saga.LinesPending.UnionWith(context.Message.Shakes.Select(x => x.ShakeId));
                    }

                    if (context.Message.FryShakes != null)
                    {
                        context.Saga.LineCount += context.Message.FryShakes.Length;
                        context.Saga.LinesPending.UnionWith(context.Message.FryShakes.Select(x => x.FryShakeId));
                    }
                })
                .LogSeeded(logger)
                .Activity(x => x.OfType<OrderBurgersActivity>())
                .Activity(x => x.OfType<OrderFriesActivity>())
                .Activity(x => x.OfType<OrderShakesActivity>())
                .Activity(x => x.OfType<OrderFryShakesActivity>())
                .LogFanOutDone(logger)
                .TransitionTo(WaitingForCompletion)
        );

        During(WaitingForCompletion,
            When(OrderSubmitted)
                .LogDuplicate(logger)
                .If(context => context.Saga.RequestId != context.RequestId, x => x.RequestStarted())
        );

        During(Completed,
            When(OrderSubmitted)
                .LogReplay(logger, "Completed")
                .RespondAsync(x => x.CreateOrderCompleted())
        );

        During(Faulted,
            When(OrderSubmitted)
                .LogReplay(logger, "Faulted")
                .RespondAsync(x => x.CreateOrderFaulted())
        );

        DuringAny(
            When(LineCompleted)
                .LogLineCompleted(logger)
                .CompleteLine()
                .CompleteOrderIfReady(this),
            When(LineFaulted)
                .LogLineFaulted(logger)
                .FaultLine()
                .CompleteOrderIfReady(this)
        );
    }

    public State WaitingForCompletion { get; } = null!;
    public State Completed { get; } = null!;
    public State Faulted { get; } = null!;

    public Event<SubmitOrder> OrderSubmitted { get; } = null!;
    public Event<OrderLineCompleted> LineCompleted { get; } = null!;
    public Event<OrderLineFaulted> LineFaulted { get; } = null!;
}


public static class OrderStateMachineExtensions
{
    public static EventActivityBinder<OrderState, OrderLineCompleted> CompleteLine(this EventActivityBinder<OrderState, OrderLineCompleted> binder)
    {
        return binder.Then(context =>
        {
            context.Saga.LinesPending.Remove(context.Message.OrderLineId);
            context.Saga.LinesFaulted.Remove(context.Message.OrderLineId);

            context.Saga.LinesCompleted[context.Message.OrderLineId] = context.Message;
        });
    }

    public static EventActivityBinder<OrderState, OrderLineFaulted> FaultLine(this EventActivityBinder<OrderState, OrderLineFaulted> binder)
    {
        return binder.Then(context =>
        {
            context.Saga.LinesPending.Remove(context.Message.OrderLineId);

            context.Saga.LinesFaulted[context.Message.OrderLineId] = context.Message;
        });
    }

    public static EventActivityBinder<OrderState, T> CompleteOrderIfReady<T>(this EventActivityBinder<OrderState, T> binder, OrderStateMachine machine)
        where T : class
    {
        return binder
            .If(context => context.Saga.LinesPending.Count == 0, ready => ready
                .IfElse(context => context.Saga.LinesFaulted.Count == 0,
                    completed => completed
                        .SetCompleted(x => x.CreateOrderCompleted())
                        .TransitionTo(machine.Completed),
                    notCompleted => notCompleted
                        .SetFaulted(x => x.CreateOrderFaulted())
                        .TransitionTo(machine.Faulted)
                )
            );
    }

    public static async Task<OrderCompleted> CreateOrderCompleted<T>(this BehaviorContext<OrderState, T> context)
        where T : class
    {
        var tuple = await context.Init<OrderCompleted>(new
        {
            context.Saga.Created,
            context.Saga.Completed,
            OrderId = context.Saga.CorrelationId,
            context.Saga.LinesCompleted
        });
        return tuple.Message;
    }

    public static async Task<OrderFaulted> CreateOrderFaulted<T>(this BehaviorContext<OrderState, T> context)
        where T : class
    {
        var tuple = await context.Init<OrderFaulted>(new
        {
            context.Saga.Created,
            context.Saga.Faulted,
            OrderId = context.Saga.CorrelationId,
            ExceptionInfo = default(ExceptionInfo),
            context.Saga.LinesCompleted,
            context.Saga.LinesFaulted
        });
        return tuple.Message;
    }
}
