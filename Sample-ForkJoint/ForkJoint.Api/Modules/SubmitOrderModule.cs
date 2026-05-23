namespace ForkJoint.Api.Modules;

public record SubmitOrderRequest(Guid OrderId, bool Lettuce);

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
                    Burgers = new[] { new Burger { Lettuce = request.Lettuce } }
                }, cancellationToken);

            if (response.Is<OrderCompleted>(out var completed))
                return Results.Ok(completed.Message);

            if (response.Is<OrderNotCompleted>(out var notCompleted))
                return Results.Problem(
                    title: "Order not completed",
                    detail: notCompleted.Message.Reason,
                    statusCode: StatusCodes.Status422UnprocessableEntity);

            return Results.Problem("Unexpected response", statusCode: StatusCodes.Status500InternalServerError);
        });
    }
}
