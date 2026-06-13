using ForkJointEnterprise.Api.Services;

namespace ForkJointEnterprise.Api.Components.Consumers;

public class PourShakeConsumer(IShakeMachine shakeMachine, ILogger<PourShakeConsumer> logger) :
    IConsumer<PourShake>
{
    public async Task Consume(ConsumeContext<PourShake> context)
    {
        logger.LogInformation("PourShakeConsumer: OrderId={OrderId} LineId={LineId} Flavor={Flavor} Size={Size}",
            context.Message.OrderId, context.Message.OrderLineId, context.Message.Flavor, context.Message.Size);

        await shakeMachine.PourShake(context.Message.Flavor, context.Message.Size);

        logger.LogInformation("PourShakeConsumer done, responding ShakeReady: LineId={LineId}", context.Message.OrderLineId);

        await context.RespondAsync<ShakeReady>(new
        {
            context.Message.OrderId,
            context.Message.OrderLineId,
            context.Message.Flavor,
            context.Message.Size
        });
    }
}
