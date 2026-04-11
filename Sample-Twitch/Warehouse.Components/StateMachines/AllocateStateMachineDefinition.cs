namespace Warehouse.Components.StateMachines
{
    using MassTransit;


    public class AllocateStateMachineDefinition :
        SagaDefinition<AllocationState>
    {
        public AllocateStateMachineDefinition()
        {
            ConcurrentMessageLimit = 10;
        }

        protected override void ConfigureSaga(IReceiveEndpointConfigurator endpointConfigurator, ISagaConfigurator<AllocationState> sagaConfigurator, 
            IRegistrationContext context)
        {
            endpointConfigurator.UseMessageRetry(r => r.Interval(3, 1000));
            //endpointConfigurator.UseInMemoryOutbox(context);
        }
    }
}