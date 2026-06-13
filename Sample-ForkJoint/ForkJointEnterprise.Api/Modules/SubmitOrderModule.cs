namespace ForkJointEnterprise.Api.Modules;

public record BurgerInput(
    bool Lettuce = false,
    bool Cheese = false,
    bool Pickle = true,
    bool Onion = true,
    bool Ketchup = false,
    bool Mustard = true,
    decimal Weight = 0.5m);

public record SubmitOrderRequest(Guid OrderId, BurgerInput[]? Burgers);

// POST /orders → IRequestClient<SubmitOrder> → OrderStateMachine (batch).
// OrderStateMachine fans out one OrderBurger per item via RequestBurgerActivity, waits for all
// OrderLineCompleted/Faulted, then replies with OrderCompleted/OrderFaulted.
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

            var inputs = request.Burgers is { Length: > 0 }
                ? request.Burgers
                : [new BurgerInput()];

            var burgers = inputs.Select(b => new Burger
            {
                BurgerId = NewId.NextGuid(),
                Lettuce = b.Lettuce,
                Cheese = b.Cheese,
                Pickle = b.Pickle,
                Onion = b.Onion,
                Ketchup = b.Ketchup,
                Mustard = b.Mustard,
                Weight = b.Weight
            }).ToArray();

            var response = await client.GetResponse<OrderCompleted, OrderFaulted>(
                new
                {
                    OrderId = orderId,
                    Burgers = burgers
                }, cancellationToken);

            if (response.Is<OrderCompleted>(out var completed))
                return Results.Ok(completed.Message);

            if (response.Is<OrderFaulted>(out var faulted))
                return Results.Problem(
                    title: "Order faulted",
                    detail: faulted.Message.ExceptionInfo?.Message ?? "Unknown",
                    statusCode: StatusCodes.Status422UnprocessableEntity);

            return Results.Problem("Unexpected response", statusCode: StatusCodes.Status500InternalServerError);
        });
    }
}
