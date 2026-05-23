namespace ForkJoint.Api.Modules;

public record RequestBurgerRequest(Guid OrderId, Burger Burger);

// POST /burgers → IRequestClient<RequestBurger> → BurgerStateMachine saga (correlated by Burger.BurgerId).
// Saga's Initially block creates a BurgerState instance, runs PrepareBurgerActivity (which builds a routing slip
// via BurgerItineraryPlanner and executes grill/dress activities), then transitions to WaitingForCompletion.
// On RoutingSlipCompleted / RoutingSlipFaulted, saga transitions to Completed / Faulted and calls
// .RequestCompleted(...) — the built-in RequestStateMachine bridges that back to this IRequestClient.
// Requires both BurgerStateMachine AND RequestStateMachine sagas registered (see Program.cs).
public class BurgerModule : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost("/burgers", async (
            RequestBurgerRequest request,
            IRequestClient<RequestBurger> client,
            CancellationToken cancellationToken) =>
        {
            var orderId = request.OrderId == Guid.Empty ? NewId.NextGuid() : request.OrderId;

            var response = await client.GetResponse<BurgerCompleted, BurgerNotCompleted>(
                new
                {
                    OrderId = orderId,
                    request.Burger,
                }, cancellationToken);

            if (response.Is<BurgerCompleted>(out var completed))
                return Results.Ok(completed.Message);

            if (response.Is<BurgerNotCompleted>(out var notCompleted))
                return Results.Problem(
                    title: "Burger not completed",
                    detail: notCompleted.Message.Reason,
                    statusCode: StatusCodes.Status422UnprocessableEntity);

            return Results.Problem("Unexpected response", statusCode: StatusCodes.Status500InternalServerError);
        });
    }
}
