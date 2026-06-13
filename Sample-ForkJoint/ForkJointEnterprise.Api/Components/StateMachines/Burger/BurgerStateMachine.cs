using MassTransit.Courier.Contracts;

namespace ForkJointEnterprise.Api.Components.StateMachines;

public class BurgerStateMachine :
    MassTransitStateMachine<BurgerState>
{
    public BurgerStateMachine(ILogger<BurgerStateMachine> logger)
    {
        Event(() => BurgerRequested, x => x.CorrelateById(context => context.Message.Burger.BurgerId));
        Event(() => BurgerCompleted, x => x.CorrelateById(instance => instance.TrackingNumber, context => context.Message.TrackingNumber));
        Event(() => BurgerFaulted, x => x.CorrelateById(instance => instance.TrackingNumber, context => context.Message.TrackingNumber));

        InstanceState(x => x.CurrentState, WaitingForCompletion, Completed, Faulted);

        Initially(
            When(BurgerRequested)
                .LogRequested(logger)
                .InitializeFuture()
                .Then(context =>
                {
                    context.Saga.OrderId = context.Message.OrderId;
                    context.Saga.Burger = context.Message.Burger;
                })
                .LogSeeded(logger)
                .Activity(x => x.OfInstanceType<PrepareBurgerActivity>())
                .LogSlipLaunched(logger)
                .TransitionTo(WaitingForCompletion)
        );

        During(WaitingForCompletion,
            When(BurgerRequested)
                .LogDuplicate(logger)
                .PendingRequestStarted()
        );

        During(Completed,
            When(BurgerRequested)
                .LogReplay(logger, "Completed")
                .RespondAsync(x => x.CreateBurgerCompleted())
        );

        During(Faulted,
            When(BurgerRequested)
                .LogReplay(logger, "Faulted")
                .RespondAsync(x => x.CreateBurgerFaulted())
        );

        DuringAny(
            When(BurgerCompleted)
                .LogBurgerCompleted(logger)
                .CompleteBurger()
                .SetCompleted(x => x.CreateBurgerCompleted())
                .TransitionTo(Completed),
            When(BurgerFaulted)
                .LogBurgerFaulted(logger)
                .FaultBurger()
                .SetFaulted(x => x.CreateBurgerFaulted())
                .TransitionTo(Faulted)
        );
    }

    public State WaitingForCompletion { get; } = null!;
    public State Completed { get; } = null!;
    public State Faulted { get; } = null!;

    public Event<OrderBurger> BurgerRequested { get; } = null!;
    public Event<RoutingSlipCompleted> BurgerCompleted { get; } = null!;
    public Event<RoutingSlipFaulted> BurgerFaulted { get; } = null!;
}


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
