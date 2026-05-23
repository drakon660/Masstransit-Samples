using MassTransit.Courier.Contracts;
using MassTransit.Initializers;

namespace ForkJoint.Api.Components.Consumers;

public class SubmitOrderResponseConsumer :
    RoutingSlipResponseConsumer<SubmitOrder, OrderCompleted, OrderNotCompleted>
{
    public SubmitOrderResponseConsumer(ILogger<SubmitOrderResponseConsumer> logger) : base(logger) { }

    protected override async Task<OrderCompleted> CreateResponseMessage(ConsumeContext<RoutingSlipCompleted> context, SubmitOrder request)
    {
        var orderId = context.GetVariable<Guid>("OrderId");

        var burger = GetRefVar<Burger>(context, "Burger");

        Logger.LogInformation("CreateResponseMessage: orderId={OrderId} burgerNull={BurgerNull}", orderId, burger == null);

        var tuple = await MessageInitializerCache<OrderCompleted>.InitializeMessage(context, new
        {
            orderId,
            burger
        });

        return tuple.Message;
    }

    protected override async Task<OrderNotCompleted> CreateFaultedResponseMessage(ConsumeContext<RoutingSlipFaulted> context, SubmitOrder request,
        Guid requestId)
    {
        var orderId = context.GetVariable<Guid>("OrderId");

        IEnumerable<ExceptionInfo> exceptions = context.Message.ActivityExceptions.Select(x => x.ExceptionInfo);

        var reason = exceptions.FirstOrDefault()?.Message ?? "Unknown";

        Logger.LogInformation("CreateFaultedResponseMessage: orderId={OrderId} requestId={RequestId} reason={Reason}", orderId, requestId, reason);

        var tuple = await MessageInitializerCache<OrderNotCompleted>.InitializeMessage(context, new
        {
            orderId,
            Reason = reason,
            request.Burgers
        });

        return tuple.Message;
    }
}