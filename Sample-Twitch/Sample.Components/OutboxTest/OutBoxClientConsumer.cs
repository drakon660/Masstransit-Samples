namespace Sample.Components.OutboxTest;

using System.Threading.Tasks;
using MassTransit;
using Microsoft.Extensions.Logging;
using Sample.Contracts;

public class OutBoxClientConsumer(ILogger<OutBoxClientConsumer> logger) : IConsumer<OutBoxMessageProcessed>
{
    private readonly ILogger<OutBoxClientConsumer> _logger = logger;

    public Task Consume(ConsumeContext<OutBoxMessageProcessed> context)
    {
        _logger.LogInformation("[OutBoxClientConsumer] OutBoxMessageProcessed received: {Id}", context.Message.Id);
        return Task.CompletedTask;
    }
}
