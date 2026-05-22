namespace ForkJoint.Api.Modules;

public record SubmitOrderRequest(Guid OrderId);

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

            var response = await client.GetResponse<OrderSubmissionAccepted>(
                new { OrderId = orderId }, cancellationToken);

            return Results.Accepted($"/orders/{response.Message.OrderId}", response.Message);
        });
    }
}
