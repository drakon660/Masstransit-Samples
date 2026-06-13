using ForkJointEnterprise.Api.Services;

namespace ForkJointEnterprise.Api.Components.Consumers;

public class CookOnionRingsConsumer(IFryer fryer, ILogger<CookOnionRingsConsumer> logger) :
    IConsumer<CookOnionRings>
{
    public async Task Consume(ConsumeContext<CookOnionRings> context)
    {
        logger.LogInformation("CookOnionRingsConsumer: OrderId={OrderId} LineId={LineId} Quantity={Quantity}",
            context.Message.OrderId, context.Message.OrderLineId, context.Message.Quantity);

        await fryer.CookOnionRings(context.Message.Quantity);

        logger.LogInformation("CookOnionRingsConsumer done, responding OnionRingsReady: LineId={LineId}", context.Message.OrderLineId);

        await context.RespondAsync<OnionRingsReady>(new
        {
            context.Message.OrderId,
            context.Message.OrderLineId,
            context.Message.Quantity
        });
    }
}
