using Library.Contracts;
using MassTransit;

namespace Library.Components.Consumers;

public class ChargeFineConsumer :
    IConsumer<ChargeMemberFine>
{
    public async Task Consume(ConsumeContext<ChargeMemberFine> context)
    {
        await Task.Delay(1000);

        if (context.Message.Amount < 150m && context.IsResponseAccepted<FineWaived>())
        {
            await context.RespondAsync<FineWaived>(new
            {
                context.Message.MemberId,
                context.Message.Amount,
                Reason = "Fine is below the collection threshold"
            });
            return;
        }

        await context.RespondAsync<FineCharged>(context.Message);
    }
}
