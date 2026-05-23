using ForkJoint.Api.Components.Activities.ItineraryPlanners;
using MassTransit.Courier.Contracts;

namespace ForkJoint.Api.Components.StateMachines;

public class PrepareBurgerActivity :
    IStateMachineActivity<BurgerState>
{
    readonly IBurgerItineraryPlanner _planner;

    public PrepareBurgerActivity(IBurgerItineraryPlanner planner)
    {
        _planner = planner;
    }

    public void Probe(ProbeContext context) => context.CreateScope("prepareBurger");

    public void Accept(StateMachineVisitor visitor) => visitor.Visit(this);

    public async Task Execute(BehaviorContext<BurgerState> context, IBehavior<BurgerState> next)
    {
        await ExecuteCore(context);
        await next.Execute(context);
    }

    public async Task Execute<T>(BehaviorContext<BurgerState, T> context, IBehavior<BurgerState, T> next) where T : class
    {
        await ExecuteCore(context);
        await next.Execute(context);
    }

    async Task ExecuteCore(BehaviorContext<BurgerState> context)
    {
        var trackingNumber = NewId.NextGuid();

        var builder = new RoutingSlipBuilder(trackingNumber);

        builder.AddSubscription(context.ReceiveContext.InputAddress, RoutingSlipEvents.Completed | RoutingSlipEvents.Faulted);

        if (context.ExpirationTime.HasValue)
            builder.AddVariable("Deadline", context.ExpirationTime.Value);

        builder.AddVariable("OrderId", context.Saga.OrderId);
        builder.AddVariable("BurgerId", context.Saga.CorrelationId);

        _planner.PlanItinerary(context.Saga.Burger, builder);

        await context.Execute(builder.Build()).ConfigureAwait(false);

        context.Saga.TrackingNumber = trackingNumber;
    }

    public Task Faulted<TException>(BehaviorExceptionContext<BurgerState, TException> context, IBehavior<BurgerState> next)
        where TException : Exception
        => next.Faulted(context);

    public Task Faulted<T, TException>(BehaviorExceptionContext<BurgerState, T, TException> context, IBehavior<BurgerState, T> next)
        where T : class
        where TException : Exception
        => next.Faulted(context);
}
