using ForkJoint.Api.Components.Activities.ItineraryPlanners;

namespace ForkJoint.Api.Components.Consumers;

public class SubmitOrderConsumer :
    RoutingSlipRequestConsumer<SubmitOrder>
{
    readonly IBurgerItineraryPlanner _planner;
    readonly ILogger<SubmitOrderConsumer> _logger;

    public SubmitOrderConsumer(IBurgerItineraryPlanner planner, IEndpointNameFormatter formatter, ILogger<SubmitOrderConsumer> logger)
        : base(formatter.Consumer<SubmitOrderResponseConsumer>())
    {
        _planner = planner;
        _logger = logger;
    }

    protected override Task BuildItinerary(RoutingSlipBuilder builder, ConsumeContext<SubmitOrder> context)
    {
        _logger.LogInformation("BuildItinerary: OrderId={OrderId} CorrelationId={CorrelationId} RequestId={RequestId} ResponseAddress={ResponseAddress} Burgers={Count}",
            context.Message.OrderId, context.CorrelationId, context.RequestId, context.ResponseAddress,
            context.Message.Burgers?.Length ?? 0);

        builder.AddVariable("OrderId", context.Message.OrderId);

        if (context.ExpirationTime.HasValue)
        {
            _logger.LogInformation("Adding Deadline variable: {Deadline}", context.ExpirationTime.Value);
            builder.AddVariable("Deadline", context.ExpirationTime.Value);
        }

        var burger = context.Message.Burgers?.FirstOrDefault();

        if (burger != null)
        {
            _logger.LogInformation("Planning itinerary for burger: Lettuce={Lettuce} Cheese={Cheese} Weight={Weight}",
                burger.Lettuce, burger.Cheese, burger.Weight);
            builder.AddVariable("Burger", burger);
            _planner.PlanItinerary(burger, builder);
        }
        else
        {
            _logger.LogWarning("No burger in request — empty itinerary.");
        }

        return Task.CompletedTask;
    }
}