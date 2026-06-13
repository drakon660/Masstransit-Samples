using MassTransit.Middleware;

namespace ForkJointEnterprise.Api.Components.StateMachines;

public class FryShakeSagaDefinition :
    SagaDefinition<FryShakeState>
{
    public FryShakeSagaDefinition()
    {
        ConcurrentMessageLimit = 32;
    }

    protected override void ConfigureSaga(IReceiveEndpointConfigurator endpointConfigurator, ISagaConfigurator<FryShakeState> sagaConfigurator,
        IRegistrationContext context)
    {
        var partitionCount = ConcurrentMessageLimit ?? Environment.ProcessorCount * 4;

        IPartitioner partitioner = new Partitioner(partitionCount, new Murmur3UnsafeHashGenerator());

        endpointConfigurator.UsePartitioner<OrderFryShake>(partitioner, x => x.Message.OrderLineId);
        endpointConfigurator.UsePartitioner<FryCompleted>(partitioner, x => x.Message.OrderLineId);
        endpointConfigurator.UsePartitioner<FryFaulted>(partitioner, x => x.Message.OrderLineId);
        endpointConfigurator.UsePartitioner<ShakeCompleted>(partitioner, x => x.Message.OrderLineId);
        endpointConfigurator.UsePartitioner<ShakeFaulted>(partitioner, x => x.Message.OrderLineId);

        endpointConfigurator.UseScheduledRedelivery(r => r.Intervals(1000));
        endpointConfigurator.UseMessageRetry(r => r.Intervals(100));
        endpointConfigurator.UseInMemoryOutbox(context);
    }
}
