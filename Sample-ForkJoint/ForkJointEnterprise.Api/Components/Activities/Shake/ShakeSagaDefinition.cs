using MassTransit.Middleware;

namespace ForkJointEnterprise.Api.Components.StateMachines;

public class ShakeSagaDefinition :
    SagaDefinition<ShakeState>
{
    public ShakeSagaDefinition()
    {
        ConcurrentMessageLimit = 32;
    }

    protected override void ConfigureSaga(IReceiveEndpointConfigurator endpointConfigurator, ISagaConfigurator<ShakeState> sagaConfigurator,
        IRegistrationContext context)
    {
        var partitionCount = ConcurrentMessageLimit ?? Environment.ProcessorCount * 4;

        IPartitioner partitioner = new Partitioner(partitionCount, new Murmur3UnsafeHashGenerator());

        endpointConfigurator.UsePartitioner<OrderShake>(partitioner, x => x.Message.OrderLineId);
        endpointConfigurator.UsePartitioner<ShakeReady>(partitioner, x => x.Message.OrderLineId);
        endpointConfigurator.UsePartitioner<Fault<PourShake>>(partitioner, x => x.Message.Message.OrderLineId);

        endpointConfigurator.UseScheduledRedelivery(r => r.Intervals(1000));
        endpointConfigurator.UseMessageRetry(r => r.Intervals(100));
        endpointConfigurator.UseInMemoryOutbox(context);
    }
}
