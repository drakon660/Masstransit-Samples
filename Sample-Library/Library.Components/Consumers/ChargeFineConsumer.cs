using Library.Contracts;
using MassTransit;

namespace Library.Components.Consumers;

public class ChargeFineConsumer :
    IConsumer<ChargeMemberFine>
{
    public async Task Consume(ConsumeContext<ChargeMemberFine> context)
    {
        await Task.Delay(1000);

        await context.RespondAsync<FineCharged>(context.Message);
    }
}