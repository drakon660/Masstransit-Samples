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