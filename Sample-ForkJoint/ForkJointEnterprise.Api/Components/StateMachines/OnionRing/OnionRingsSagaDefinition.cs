using MassTransit.Middleware;

namespace ForkJointEnterprise.Api.Components.StateMachines;

public class OnionRingsSagaDefinition :
    SagaDefinition<OnionRingsState>
{
    public OnionRingsSagaDefinition()
    {
        ConcurrentMessageLimit = 32;
    }

    protected override void ConfigureSaga(IReceiveEndpointConfigurator endpointConfigurator, ISagaConfigurator<OnionRingsState> sagaConfigurator,
        IRegistrationContext context)
    {
        var partitionCount = ConcurrentMessageLimit ?? Environment.ProcessorCount * 4;

        IPartitioner partitioner = new Partitioner(partitionCount, new Murmur3UnsafeHashGenerator());

        endpointConfigurator.UsePartitioner<OrderOnionRings>(partitioner, x => x.Message.OrderLineId);
        endpointConfigurator.UsePartitioner<OnionRingsReady>(partitioner, x => x.Message.OrderLineId);
        endpointConfigurator.UsePartitioner<Fault<CookOnionRings>>(partitioner, x => x.Message.Message.OrderLineId);

        endpointConfigurator.UseScheduledRedelivery(r => r.Intervals(1000));
        endpointConfigurator.UseMessageRetry(r => r.Intervals(100));
        endpointConfigurator.UseInMemoryOutbox(context);
    }
}
