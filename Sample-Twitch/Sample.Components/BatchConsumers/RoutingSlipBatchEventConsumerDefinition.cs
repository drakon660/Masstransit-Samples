namespace Sample.Components.BatchConsumers
{
    using MassTransit;


    public class RoutingSlipBatchEventConsumerDefinition :
        ConsumerDefinition<RoutingSlipBatchEventConsumer>
    {
        public RoutingSlipBatchEventConsumerDefinition()
        {
            Endpoint(e => e.PrefetchCount = 20);
        }

        protected override void ConfigureConsumer(IReceiveEndpointConfigurator endpointConfigurator,
            IConsumerConfigurator<RoutingSlipBatchEventConsumer> consumerConfigurator, IRegistrationContext context)
        {
            consumerConfigurator.Options<BatchOptions>(o => o.SetMessageLimit(10).SetTimeLimit(5000));
        }
    }
}