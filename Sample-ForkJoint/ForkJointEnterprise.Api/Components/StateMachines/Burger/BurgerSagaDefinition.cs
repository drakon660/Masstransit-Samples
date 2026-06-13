using MassTransit.Courier.Contracts;
using MassTransit.Middleware;

namespace ForkJointEnterprise.Api.Components.StateMachines;

public class BurgerSagaDefinition :
    SagaDefinition<BurgerState>
{
    public BurgerSagaDefinition()
    {
        var partitionCount = 32;

        ConcurrentMessageLimit = partitionCount;
    }
    
    protected override void ConfigureSaga(IReceiveEndpointConfigurator endpointConfigurator, ISagaConfigurator<BurgerState> sagaConfigurator,
        IRegistrationContext context)
    {
        // if (endpointConfigurator is IRabbitMqReceiveEndpointConfigurator rabbit)
        // {
        //     endpointConfigurator.ConfigureConsumeTopology = false;
        //     rabbit.Bind<RequestBurger>();
        // }

        var partitionCount = ConcurrentMessageLimit ?? Environment.ProcessorCount * 4;

        IPartitioner partitioner = new Partitioner(partitionCount, new Murmur3UnsafeHashGenerator());

        endpointConfigurator.UsePartitioner<OrderBurger>(partitioner, x => x.Message.Burger.BurgerId);
        endpointConfigurator.UsePartitioner<RoutingSlipCompleted>(partitioner, x => x.Message.TrackingNumber);
        endpointConfigurator.UsePartitioner<RoutingSlipFaulted>(partitioner, x => x.Message.TrackingNumber);
    }
}