namespace Sample.Components.Consumers
{
    using System;
    using MassTransit;


    public class FulfillOrderConsumerDefinition :
        ConsumerDefinition<FulfillOrderConsumer>
    {
        public FulfillOrderConsumerDefinition()
        {
            ConcurrentMessageLimit = 20;
        }

        protected override void ConfigureConsumer(IReceiveEndpointConfigurator endpointConfigurator,
            IConsumerConfigurator<FulfillOrderConsumer> consumerConfigurator, IRegistrationContext context)
        {
            endpointConfigurator.UseMessageRetry(r =>
            {
                r.Interval(3, 1000);
            });

            //endpointConfigurator.DiscardFaultedMessages();
        }
    }
}