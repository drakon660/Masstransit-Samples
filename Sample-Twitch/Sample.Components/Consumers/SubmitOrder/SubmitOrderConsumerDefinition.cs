namespace Sample.Components.Consumers
{
    using System;
    using Contracts;
    using MassTransit;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;


    public class SubmitOrderConsumerDefinition :
        ConsumerDefinition<SubmitOrderConsumer>
    {
        readonly ILogger<SubmitOrderConsumerDefinition> _logger;

        public SubmitOrderConsumerDefinition(ILogger<SubmitOrderConsumerDefinition> logger)
        {
            _logger = logger;

            // Max parallel messages processed on this endpoint
            ConcurrentMessageLimit = 20;

            // Custom endpoint name (overrides kebab-case convention)
            // EndpointName = "my-custom-queue";

            // Prefetch — how many messages RabbitMQ pushes to consumer at once
            // Endpoint(e => e.PrefetchCount = 50);

            // Temporary queue — auto-deletes when consumer disconnects
            // Endpoint(e => e.Temporary = true);
        }

        protected override void ConfigureConsumer(IReceiveEndpointConfigurator endpointConfigurator,
            IConsumerConfigurator<SubmitOrderConsumer> consumerConfigurator, IRegistrationContext context)
        {
            // --- ENDPOINT level — runs for every message on THIS endpoint ---

            //endpointConfigurator.UseMessageRetry(r => r.Interval(3, 1000));

            endpointConfigurator.UseConsumeFilter(typeof(ContainerScopedFilter<>), context);

            endpointConfigurator.UseInMemoryOutbox(context);
            
            // Circuit breaker — stops consuming after too many failures, gives dependencies time to recover
            // endpointConfigurator.UseCircuitBreaker(cb =>
            // {
            //     cb.TrackingPeriod = TimeSpan.FromMinutes(1);
            //     cb.TripThreshold = 15;     // % of failures to trip
            //     cb.ActiveThreshold = 10;   // min messages before evaluating
            //     cb.ResetInterval = TimeSpan.FromMinutes(5);
            // });

            // Rate limiter — max N messages per interval
            // endpointConfigurator.UseRateLimit(100, TimeSpan.FromSeconds(1));

            // Scheduled redelivery — retry with increasing delays via Quartz scheduler
            // endpointConfigurator.UseScheduledRedelivery(r =>
            //     r.Intervals(
            //         TimeSpan.FromMinutes(1),
            //         TimeSpan.FromMinutes(5),
            //         TimeSpan.FromMinutes(15)));

            // In-memory outbox — prevents duplicate publishes on retry
            // (if consumer publishes OrderSubmitted then fails, without outbox you get duplicates)
            // endpointConfigurator.UseInMemoryOutbox(context);

            // Kill switch — stops the entire endpoint if error rate spikes
            // endpointConfigurator.UseKillSwitch(options => options
            //     .SetActivationThreshold(10)
            //     .SetTripThreshold(0.15)
            //     .SetRestartTimeout(TimeSpan.FromMinutes(1)));

            // Discard faulted messages — don't move to _error queue, just drop them
            // endpointConfigurator.DiscardFaultedMessages();

            // --- MESSAGE level — runs only for SubmitOrder messages on this endpoint ---

            consumerConfigurator.Message<SubmitOrder>(x => x.UseExecute(m =>
               _logger.LogInformation("Message received: {Message}", m.Message)));
        }
    }
}
