namespace ForkJointEnterprise.Api.Components.StateMachines;

public static class OrderStateMachineLoggingExtensions
{
    public static EventActivityBinder<OrderState, SubmitOrder> LogSubmitted(
        this EventActivityBinder<OrderState, SubmitOrder> binder, ILogger logger)
    {
        return binder.Then(context => logger.LogInformation(
            "OrderStateMachine[Initial] OrderSubmitted: OrderId={OrderId} Burgers={Count} RequestId={RequestId}",
            context.Message.OrderId, context.Message.Burgers?.Length ?? 0, context.RequestId));
    }

    public static EventActivityBinder<OrderState, SubmitOrder> LogSeeded(
        this EventActivityBinder<OrderState, SubmitOrder> binder, ILogger logger)
    {
        return binder.Then(context => logger.LogInformation(
            "OrderStateMachine[Initial] saga seeded: LineCount={LineCount} PendingIds=[{Ids}]",
            context.Saga.LineCount, string.Join(",", context.Saga.LinesPending)));
    }

    public static EventActivityBinder<OrderState, SubmitOrder> LogFanOutDone(
        this EventActivityBinder<OrderState, SubmitOrder> binder, ILogger logger)
    {
        return binder.Then(context => logger.LogInformation(
            "OrderStateMachine[Initial] RequestBurgerActivity fan-out done: OrderId={OrderId}",
            context.Saga.CorrelationId));
    }

    public static EventActivityBinder<OrderState, SubmitOrder> LogDuplicate(
        this EventActivityBinder<OrderState, SubmitOrder> binder, ILogger logger)
    {
        return binder.Then(context => logger.LogInformation(
            "OrderStateMachine[Waiting] duplicate OrderSubmitted: OrderId={OrderId} sagaReq={SagaReq} ctxReq={CtxReq}",
            context.Message.OrderId, context.Saga.RequestId, context.RequestId));
    }

    public static EventActivityBinder<OrderState, SubmitOrder> LogReplay(
        this EventActivityBinder<OrderState, SubmitOrder> binder, ILogger logger, string state)
    {
        return binder.Then(context => logger.LogInformation(
            "OrderStateMachine[{State}] replay OrderSubmitted: OrderId={OrderId}",
            state, context.Message.OrderId));
    }

    public static EventActivityBinder<OrderState, OrderLineCompleted> LogLineCompleted(
        this EventActivityBinder<OrderState, OrderLineCompleted> binder, ILogger logger)
    {
        return binder.Then(context => logger.LogInformation(
            "OrderStateMachine LineCompleted: OrderId={OrderId} OrderLineId={OrderLineId} Pending(before)={Pending}",
            context.Message.OrderId, context.Message.OrderLineId, context.Saga.LinesPending.Count));
    }

    public static EventActivityBinder<OrderState, OrderLineFaulted> LogLineFaulted(
        this EventActivityBinder<OrderState, OrderLineFaulted> binder, ILogger logger)
    {
        return binder.Then(context => logger.LogError(
            "OrderStateMachine LineFaulted: OrderId={OrderId} OrderLineId={OrderLineId} Pending(before)={Pending}",
            context.Message.OrderId, context.Message.OrderLineId, context.Saga.LinesPending.Count));
    }
}
