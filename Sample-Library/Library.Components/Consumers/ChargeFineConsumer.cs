using Library.Contracts;
using MassTransit;

namespace Library.Components.Consumers;

public class ChargeFineConsumer :
    IConsumer<ChargeMemberFine>
{
    public async Task Consume(ConsumeContext<ChargeMemberFine> context)
    {
        await Task.Delay(1000);

        // The request client must declare FineWaived as an accepted response type before we can return it.
        if (context.Message.MemberAge < 18 && context.IsResponseAccepted<FineWaived>())
        {
            await context.RespondAsync<FineWaived>(new
            {
                context.Message.MemberId,
                context.Message.Amount,
                Reason = "Member is under 18"
            });
            return;
        }

        await context.RespondAsync<FineCharged>(context.Message);
    }
}
