using System;
using System.Threading.Tasks;
using MassTransit;
using Microsoft.Extensions.Logging;
using Sample.Contracts;

namespace Sample.Components.OutboxTest;

public class OutBoxConsumer(ILogger<OutBoxConsumer> logger) : IConsumer<OutBoxMessage>
{
    private readonly ILogger<OutBoxConsumer> _logger = logger;

    public async Task Consume(ConsumeContext<OutBoxMessage> context)
    {
        var attempt = context.GetRetryAttempt();

        _logger.LogInformation(
            "[OutBoxConsumer] received {Id} (retry attempt #{Attempt})",
            context.Message.Id, attempt);

        // Publish BEFORE the throw. With UseInMemoryInboxOutbox this is buffered
        // and only flushed if the consumer returns successfully. Without it,
        // the publish goes straight to the broker and OutBoxClientConsumer
        // sees the event twice (once per attempt).
        await context.Publish<OutBoxMessageProcessed>(new { context.Message.Id });

        _logger.LogInformation(
            "[OutBoxConsumer] published OutBoxMessageProcessed for {Id}",
            context.Message.Id);

        if (attempt == 0)
        {
            _logger.LogWarning(
                "[OutBoxConsumer] simulated failure on initial attempt for {Id}",
                context.Message.Id);
            throw new InvalidOperationException("simulated failure on initial attempt");
        }

        _logger.LogInformation("[OutBoxConsumer] consume succeeded for {Id}", context.Message.Id);
    }
}

public class OutBoxConsumerDefinition : ConsumerDefinition<OutBoxConsumer>
{
    protected override void ConfigureConsumer(
        IReceiveEndpointConfigurator endpointConfigurator,
        IConsumerConfigurator<OutBoxConsumer> consumerConfigurator,
        IRegistrationContext context)
    {
        // Retry once after the simulated initial failure so the second attempt succeeds.
        endpointConfigurator.UseMessageRetry(r => r.Immediate(2));

        // In-memory outbox: buffers Publish/Send until the consumer commits successfully.
        // Comment this line out to observe OutBoxClientConsumer receiving the event TWICE
        // (once from the failed attempt, once from the successful retry). With it enabled,
        // the publish from the failed attempt is discarded and the client only sees one event.
        //
        // NOTE: this is the older outbox-only API. The newer combined UseInMemoryInboxOutbox
        // is broken when applied at the receive-endpoint scope (throws "Instances of abstract
        // classes cannot be created" at runtime). UseInMemoryOutbox is the documented, stable
        // call — it gives you the publish-buffering half but not broker-redelivery dedup.
        endpointConfigurator.UseInMemoryOutbox(context);
    }
}
