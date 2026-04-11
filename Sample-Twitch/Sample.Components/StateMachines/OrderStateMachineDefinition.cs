namespace Sample.Components.StateMachines
{
    using System;
    using MassTransit;


    public class OrderStateMachineDefinition :
        SagaDefinition<OrderState>
    {
        public OrderStateMachineDefinition()
        {
            // Max parallel saga messages — important for sagas to prevent concurrency issues
            ConcurrentMessageLimit = 12;

            // Custom endpoint name (overrides kebab-case convention)
            // EndpointName = "order-state";

            // Prefetch — how many messages RabbitMQ pushes at once
            // Endpoint(e => e.PrefetchCount = 20);
        }

        protected override void ConfigureSaga(IReceiveEndpointConfigurator endpointConfigurator,
            ISagaConfigurator<OrderState> sagaConfigurator, IRegistrationContext context)
        {
            // --- ENDPOINT level — runs for every message on the saga endpoint ---

            // Retry with increasing intervals — critical for sagas due to optimistic concurrency conflicts
            endpointConfigurator.UseMessageRetry(r => r.Intervals(500, 5000, 10000));

            // In-memory outbox — ensures events published during saga transitions are only sent
            // after the saga state is successfully persisted. Prevents ghost events on failed saves.
            //endpointConfigurator.UseInMemoryOutbox(context);

            // Circuit breaker — pause saga processing when repository (Redis/Mongo) is down
            // endpointConfigurator.UseCircuitBreaker(cb =>
            // {
            //     cb.TrackingPeriod = TimeSpan.FromMinutes(1);
            //     cb.TripThreshold = 15;
            //     cb.ActiveThreshold = 10;
            //     cb.ResetInterval = TimeSpan.FromMinutes(5);
            // });

            // Scheduled redelivery — for long-running sagas, redeliver after delays via Quartz
            // endpointConfigurator.UseScheduledRedelivery(r =>
            //     r.Intervals(
            //         TimeSpan.FromMinutes(1),
            //         TimeSpan.FromMinutes(5),
            //         TimeSpan.FromMinutes(15)));

            // Partitioning — ensures messages for the same saga instance are processed in order
            // Useful when ConcurrentMessageLimit is high and ordering matters
            // endpointConfigurator.UsePartitioner(16, context => context.CorrelationId ?? NewId.NextGuid());

            // --- SAGA level — runs within the saga scope ---

            // Scoped filter on the saga — e.g. for authorization or tenant resolution per saga event
            // sagaConfigurator.UseFilter(new MySagaFilter());
        }
    }
}
