using ForkJointEnterprise.Api.Components.Activities.ItineraryPlanners;
using MassTransit.Courier.Contracts;

namespace ForkJointEnterprise.Api.Components.StateMachines;

public class PrepareBurgerActivity :
    IStateMachineActivity<BurgerState>
{
    readonly IBurgerItineraryPlanner _planner;
    readonly ILogger<PrepareBurgerActivity> _logger;

    public PrepareBurgerActivity(IBurgerItineraryPlanner planner, ILogger<PrepareBurgerActivity> logger)
    {
        _planner = planner;
        _logger = logger;
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

        _logger.LogInformation("PrepareBurgerActivity: BurgerId={BurgerId} OrderId={OrderId} TrackingNumber={TrackingNumber}",
            context.Saga.CorrelationId, context.Saga.OrderId, trackingNumber);

        var builder = new RoutingSlipBuilder(trackingNumber);

        builder.AddSubscription(context.ReceiveContext.InputAddress, RoutingSlipEvents.Completed | RoutingSlipEvents.Faulted);

        if (context.ExpirationTime.HasValue)
            builder.AddVariable("Deadline", context.ExpirationTime.Value);

        builder.AddVariable("OrderId", context.Saga.OrderId);
        builder.AddVariable("BurgerId", context.Saga.CorrelationId);

        _logger.LogInformation("PrepareBurgerActivity planning itinerary: Lettuce={Lettuce} Cheese={Cheese} Weight={Weight}",
            context.Saga.Burger?.Lettuce, context.Saga.Burger?.Cheese, context.Saga.Burger?.Weight);

        _planner.PlanItinerary(context.Saga.Burger, builder);

        if (context.Saga.Burger is { OnionRing: true })
        {
            await context.Publish<OrderOnionRings>(new
            {
                context.Saga.OrderId,
                OrderLineId = context.Saga.Burger.BurgerId,
                Quantity = 1
            });
        }

        await context.Execute(builder.Build()).ConfigureAwait(false);

        context.Saga.TrackingNumber = trackingNumber;

        _logger.LogInformation("PrepareBurgerActivity slip dispatched: TrackingNumber={TrackingNumber}", trackingNumber);
    }

    public Task Faulted<TException>(BehaviorExceptionContext<BurgerState, TException> context, IBehavior<BurgerState> next)
        where TException : Exception
        => next.Faulted(context);

    public Task Faulted<T, TException>(BehaviorExceptionContext<BurgerState, T, TException> context, IBehavior<BurgerState, T> next)
        where T : class
        where TException : Exception
        => next.Faulted(context);
}
