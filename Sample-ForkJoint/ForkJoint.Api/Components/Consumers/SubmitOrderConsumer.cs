using ForkJoint.Api.Components.Activities.DressBurger;
using ForkJoint.Api.Components.Activities.GrillBurger;
using ForkJoint.Contracts;
using MassTransit;
using MassTransit.Courier.Contracts;

namespace ForkJoint.Api.Components.Consumers;

public class SubmitOrderConsumer :
    IConsumer<SubmitOrder>
{
    readonly IEndpointNameFormatter _formatter;
    readonly ILogger<SubmitOrderConsumer> _logger;

    public SubmitOrderConsumer(ILogger<SubmitOrderConsumer> logger, IEndpointNameFormatter formatter)
    {
        _logger = logger;
        _formatter = formatter;
    }

    public async Task Consume(ConsumeContext<SubmitOrder> context)
    {
        _logger.LogInformation("Order Submission Received: {OrderId} {CorrelationId}", context.Message.OrderId, context.CorrelationId);

        var routingSlip = CreateRoutingSlip(context.Message);

        await context.Execute(routingSlip);

        if (context.ResponseAddress != null)
            await context.RespondAsync<OrderSubmissionAccepted>(new {context.Message.OrderId});
    }

    private RoutingSlip CreateRoutingSlip(SubmitOrder submitOrder)
    {
        var builder = new RoutingSlipBuilder(NewId.NextGuid());

        builder.AddVariable("OrderId", submitOrder.OrderId);
        
        var grillQueueName = _formatter.ExecuteActivity<GrillBurgerActivity, GrillBurgerArguments>();
        builder.AddActivity("grill-burger", new Uri($"queue:{grillQueueName}"), new
        {
            Weight = 0.5m,
            Temperature = 165.0m
        });

        var dressQueueName = _formatter.ExecuteActivity<DressBurgerActivity, DressBurgerArguments>();
        builder.AddActivity("dress-burger", new Uri($"queue:{dressQueueName}"), new
        {
            Ketchup = true,
            Lettuce = true,
        });

        return builder.Build();
    }
}