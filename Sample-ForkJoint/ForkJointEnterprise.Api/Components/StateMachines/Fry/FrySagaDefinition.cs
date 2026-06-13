using MassTransit.Middleware;

namespace ForkJointEnterprise.Api.Components.StateMachines;

public class FrySagaDefinition :
    SagaDefinition<FryState>
{
    public FrySagaDefinition()
    {
        ConcurrentMessageLimit = 32;
    }

    protected override void ConfigureSaga(IReceiveEndpointConfigurator endpointConfigurator, ISagaConfigurator<FryState> sagaConfigurator,
        IRegistrationContext context)
    {
        var partitionCount = ConcurrentMessageLimit ?? Environment.ProcessorCount * 4;

        IPartitioner partitioner = new Partitioner(partitionCount, new Murmur3UnsafeHashGenerator());

        endpointConfigurator.UsePartitioner<OrderFry>(partitioner, x => x.Message.OrderLineId);
        endpointConfigurator.UsePartitioner<FryReady>(partitioner, x => x.Message.OrderLineId);
        endpointConfigurator.UsePartitioner<Fault<CookFry>>(partitioner, x => x.Message.Message.OrderLineId);

        endpointConfigurator.UseScheduledRedelivery(r => r.Intervals(1000));
        endpointConfigurator.UseMessageRetry(r => r.Intervals(100));
        endpointConfigurator.UseInMemoryOutbox(context);
    }
}
