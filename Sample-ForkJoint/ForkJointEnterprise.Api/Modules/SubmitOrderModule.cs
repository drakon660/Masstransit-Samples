using System.ComponentModel.DataAnnotations;

namespace ForkJointEnterprise.Api.Modules;

public class Order
{
    [Required]
    public Guid OrderId { get; init; }

    public Burger[] Burgers { get; init; } = [];
    public Fry[] Fries { get; init; } = [];
    public Shake[] Shakes { get; init; } = [];
    public FryShake[] FryShakes { get; init; } = [];
}

public record OrderLineCompletedDto(DateTime? Created, DateTime? Completed, string Description);

public record OrderLineFaultedDto(DateTime? Created, DateTime? Faulted, string Description, string Reason);

public class SubmitOrderModule : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost("/orders", async (
            Order request,
            IRequestClient<SubmitOrder> client,
            CancellationToken cancellationToken) =>
        {
            var orderId = request.OrderId == Guid.Empty ? NewId.NextGuid() : request.OrderId;
            
            try
            {
                var response = await client.GetResponse<OrderCompleted, OrderFaulted>(
                    new
                    {
                        OrderId = orderId,
                        Burgers = request.Burgers,
                        Fries = request.Fries,
                        Shakes = request.Shakes,
                        FryShakes = request.FryShakes
                    }, cancellationToken);

                if (response.Is<OrderCompleted>(out var completed))
                    return Results.Ok(new
                    {
                        completed.Message.OrderId,
                        completed.Message.Created,
                        completed.Message.Completed,
                        LinesCompleted = completed.Message.LinesCompleted.ToDictionary(
                            x => x.Key,
                            x => new OrderLineCompletedDto(x.Value.Created, x.Value.Completed, x.Value.Description))
                    });

                if (response.Is<OrderFaulted>(out var faulted))
                    return Results.BadRequest(new
                    {
                        faulted.Message.OrderId,
                        faulted.Message.Created,
                        faulted.Message.Faulted,
                        LinesCompleted = faulted.Message.LinesCompleted.ToDictionary(
                            x => x.Key,
                            x => new OrderLineCompletedDto(x.Value.Created, x.Value.Completed, x.Value.Description)),
                        LinesFaulted = faulted.Message.LinesFaulted.ToDictionary(
                            x => x.Key,
                            x => new OrderLineFaultedDto(x.Value.Created, x.Value.Faulted, x.Value.Description, x.Value.ExceptionInfo.Message))
                    });

                return Results.Problem("Unexpected response", statusCode: StatusCodes.Status500InternalServerError);
            }
            catch (RequestTimeoutException)
            {
                return Results.Accepted(null, new
                {
                    OrderId = orderId,
                    Accepted = request.Burgers.Select(x => x.BurgerId).ToArray()
                });
            }
        });
    }
}
