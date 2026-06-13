using ForkJointEnterprise.Api.Services;

namespace ForkJointEnterprise.Api.Components.Consumers;

public class CookFryConsumer(IFryer fryer, ILogger<CookFryConsumer> logger) :
    IConsumer<CookFry>
{
    public async Task Consume(ConsumeContext<CookFry> context)
    {
        logger.LogInformation("CookFryConsumer: OrderId={OrderId} LineId={LineId} Size={Size}",
            context.Message.OrderId, context.Message.OrderLineId, context.Message.Size);

        await fryer.CookFry(context.Message.Size);

        logger.LogInformation("CookFryConsumer done, responding FryReady: LineId={LineId}", context.Message.OrderLineId);

        await context.RespondAsync<FryReady>(new
        {
            context.Message.OrderId,
            context.Message.OrderLineId,
            context.Message.Size
        });
    }
}
