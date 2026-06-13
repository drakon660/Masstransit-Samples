using MassTransit.Middleware;

namespace ForkJointEnterprise.Api.Components.StateMachines;

public class OrderSagaDefinition :
    SagaDefinition<OrderState>
{
    public OrderSagaDefinition()
    {
        ConcurrentMessageLimit = 16;
    }

    protected override void ConfigureSaga(IReceiveEndpointConfigurator endpointConfigurator,
        ISagaConfigurator<OrderState> sagaConfigurator,
        IRegistrationContext context)
    {
        var partitionCount = ConcurrentMessageLimit ?? Environment.ProcessorCount * 4;

        IPartitioner partitioner = new Partitioner(partitionCount, new Murmur3UnsafeHashGenerator());

        endpointConfigurator.UsePartitioner<SubmitOrder>(partitioner, x => x.Message.OrderId);
        endpointConfigurator.UsePartitioner<OrderLineCompleted>(partitioner, x => x.Message.OrderId);
        endpointConfigurator.UsePartitioner<OrderLineFaulted>(partitioner, x => x.Message.OrderId);
    }
}
