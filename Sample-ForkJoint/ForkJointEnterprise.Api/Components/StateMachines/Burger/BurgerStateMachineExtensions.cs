using MassTransit.Courier.Contracts;

namespace ForkJointEnterprise.Api.Components.StateMachines;

public static class BurgerStateMachineExtensions
{
    public static EventActivityBinder<BurgerState, RoutingSlipCompleted> CompleteBurger(this EventActivityBinder<BurgerState, RoutingSlipCompleted> binder)
    {
        return binder.Then(context => context.Saga.Burger = context.GetVariable<Burger>("Burger") ?? context.Saga.Burger);
    }

    public static EventActivityBinder<BurgerState, RoutingSlipFaulted> FaultBurger(this EventActivityBinder<BurgerState, RoutingSlipFaulted> binder)
    {
        return binder.Then(context => context.Saga.ExceptionInfo = context.Message.ActivityExceptions.Select(x => x.ExceptionInfo).FirstOrDefault()!);
    }

    public static async Task<BurgerCompleted> CreateBurgerCompleted<T>(this BehaviorContext<BurgerState, T> context)
        where T : class
    {
        var tuple = await context.Init<BurgerCompleted>(new
        {
            context.Saga.Created,
            context.Saga.Completed,
            context.Saga.OrderId,
            OrderLineId = context.Saga.CorrelationId,
            Description = context.Saga.Burger?.ToString() ?? "",
            context.Saga.Burger
        });
        return tuple.Message;
    }

    public static async Task<BurgerFaulted> CreateBurgerFaulted<T>(this BehaviorContext<BurgerState, T> context)
        where T : class
    {
        var tuple = await context.Init<BurgerFaulted>(new
        {
            context.Saga.Created,
            context.Saga.Faulted,
            context.Saga.OrderId,
            OrderLineId = context.Saga.CorrelationId,
            Description = context.Saga.Burger?.ToString() ?? "",
            context.Saga.ExceptionInfo
        });
        return tuple.Message;
    }
}