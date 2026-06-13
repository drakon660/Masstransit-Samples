using MassTransit.Courier.Contracts;

namespace ForkJointEnterprise.Api.Components.StateMachines;

public static class BurgerStateMachineLoggingExtensions
{
    public static EventActivityBinder<BurgerState, OrderBurger> LogRequested(
        this EventActivityBinder<BurgerState, OrderBurger> binder, ILogger logger)
    {
        return binder.Then(context => logger.LogInformation(
            "BurgerStateMachine[Initial] BurgerRequested: BurgerId={BurgerId} OrderId={OrderId} RequestId={RequestId}",
            context.Message.Burger.BurgerId, context.Message.OrderId, context.RequestId));
    }

    public static EventActivityBinder<BurgerState, OrderBurger> LogSeeded(
        this EventActivityBinder<BurgerState, OrderBurger> binder, ILogger logger)
    {
        return binder.Then(context => logger.LogInformation(
            "BurgerStateMachine[Initial] saga seeded: CorrelationId={CorrelationId}", context.Saga.CorrelationId));
    }

    public static EventActivityBinder<BurgerState, OrderBurger> LogSlipLaunched(
        this EventActivityBinder<BurgerState, OrderBurger> binder, ILogger logger)
    {
        return binder.Then(context => logger.LogInformation(
            "BurgerStateMachine[Initial] slip launched: TrackingNumber={TrackingNumber}", context.Saga.TrackingNumber));
    }

    public static EventActivityBinder<BurgerState, OrderBurger> LogDuplicate(
        this EventActivityBinder<BurgerState, OrderBurger> binder, ILogger logger)
    {
        return binder.Then(context => logger.LogInformation(
            "BurgerStateMachine[Waiting] duplicate BurgerRequested: BurgerId={BurgerId} RequestId={RequestId}",
            context.Message.Burger.BurgerId, context.RequestId));
    }

    public static EventActivityBinder<BurgerState, OrderBurger> LogReplay(
        this EventActivityBinder<BurgerState, OrderBurger> binder, ILogger logger, string state)
    {
        return binder.Then(context => logger.LogInformation(
            "BurgerStateMachine[{State}] replay BurgerRequested: BurgerId={BurgerId}", state, context.Message.Burger.BurgerId));
    }

    public static EventActivityBinder<BurgerState, RoutingSlipCompleted> LogBurgerCompleted(
        this EventActivityBinder<BurgerState, RoutingSlipCompleted> binder, ILogger logger)
    {
        return binder.Then(context => logger.LogInformation(
            "BurgerStateMachine BurgerCompleted: TrackingNumber={TrackingNumber} BurgerId={BurgerId} Duration={Duration}",
            context.Message.TrackingNumber, context.Saga.CorrelationId, context.Message.Duration));
    }

    public static EventActivityBinder<BurgerState, RoutingSlipFaulted> LogBurgerFaulted(
        this EventActivityBinder<BurgerState, RoutingSlipFaulted> binder, ILogger logger)
    {
        return binder.Then(context => logger.LogError(
            "BurgerStateMachine BurgerFaulted: TrackingNumber={TrackingNumber} BurgerId={BurgerId} Exceptions={Count}",
            context.Message.TrackingNumber, context.Saga.CorrelationId, context.Message.ActivityExceptions.Length));
    }
}
