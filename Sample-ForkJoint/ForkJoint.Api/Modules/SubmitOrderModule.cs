namespace ForkJoint.Api.Modules;

public record SubmitOrderRequest(Guid OrderId, bool Lettuce);

public record OrderResultDto(Guid OrderId, Burger[] LinesCompleted, Burger[] LinesFaulted, string? Reason);

// POST /orders → IRequestClient<SubmitOrder> → SubmitOrderConsumer (RoutingSlipRequestConsumer<SubmitOrder>).
// The consumer builds a routing slip via BurgerItineraryPlanner and executes activities (grill, dress).
// Response (OrderCompleted / OrderNotCompleted) is sent back by SubmitOrderResponseConsumer
// (a RoutingSlipResponseConsumer subscribed to RoutingSlipCompleted / RoutingSlipFaulted).
// No saga state machine is involved — pure consumer + routing slip orchestration.
public class SubmitOrderModule : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost("/orders", async (
            SubmitOrderRequest request,
            IRequestClient<SubmitOrder> client,
            CancellationToken cancellationToken) =>
        {
            var orderId = request.OrderId == Guid.Empty ? NewId.NextGuid() : request.OrderId;

            var response = await client.GetResponse<OrderCompleted, OrderNotCompleted>(
                new
                {
                    OrderId = orderId,
                    Burgers = new[] { new Burger { BurgerId = NewId.NextGuid(), Lettuce = request.Lettuce } }
                }, cancellationToken);

            if (response.Is<OrderCompleted>(out var completed))
                return Results.Ok(new OrderResultDto(
                    completed.Message.OrderId,
                    LinesCompleted: new[] { completed.Message.Burger },
                    LinesFaulted: Array.Empty<Burger>(),
                    Reason: null));

            if (response.Is<OrderNotCompleted>(out var notCompleted))
                return Results.Ok(new OrderResultDto(
                    notCompleted.Message.OrderId,
                    LinesCompleted: Array.Empty<Burger>(),
                    LinesFaulted: notCompleted.Message.Burgers ?? Array.Empty<Burger>(),
                    Reason: notCompleted.Message.Reason));

            return Results.Ok(new OrderResultDto(orderId, Array.Empty<Burger>(), Array.Empty<Burger>(), "Unexpected response"));
        });
    }
}
