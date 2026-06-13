using ForkJointEnterprise.Api.Components.StateMachines.Order;

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
                    if (context.Message.Burgers != null)
                    {
                        context.Saga.LineCount = context.Message.Burgers.Length;
                        context.Saga.LinesPending.UnionWith(context.Message.Burgers.Select(x => x.BurgerId));
                    }
                })
                .LogSeeded(logger)
                .Activity(x => x.OfType<RequestBurgerActivity>())
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

            context.Saga.LinesCompleted.Add(context.Message.OrderLineId, context.Message);
        });
    }

    public static EventActivityBinder<OrderState, OrderLineFaulted> FaultLine(this EventActivityBinder<OrderState, OrderLineFaulted> binder)
    {
        return binder.Then(context =>
        {
            context.Saga.LinesPending.Remove(context.Message.OrderLineId);

            context.Saga.LinesFaulted.Add(context.Message.OrderLineId, context.Message);
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
