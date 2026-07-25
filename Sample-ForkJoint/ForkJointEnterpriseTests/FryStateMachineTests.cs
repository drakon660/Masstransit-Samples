using AwesomeAssertions;
using ForkJointEnterprise.Api;
using ForkJointEnterprise.Api.Components.Consumers;
using ForkJointEnterprise.Api.Components.StateMachines;
using ForkJointEnterprise.Contracts;
using MassTransit;
using MassTransit.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace ForkJointEnterpriseTests;

public class FryStateMachineTests
{
    public FryStateMachineTests(ITestOutputHelper testOutputHelper)
    {
        TestOutputRelay.Use(testOutputHelper);
    }
    
    [Fact]
    public async Task Should_Complete_Fry_Saga()
    {
        await using var provider = CreateProvider();

        var harness = provider.GetTestHarness();
        await harness.Start();

        var sagaHarness = harness.GetSagaStateMachineHarness<FryStateMachine, FryState>();

        var orderId = NewId.NextGuid();
        var orderLineId = NewId.NextGuid();

        await harness.Bus.Publish<OrderFry>(new
        {
            OrderId = orderId,
            OrderLineId = orderLineId,
            Size = Size.Medium
        }, TestContext.Current.CancellationToken);

        await harness.AssertConsumed<OrderFry>("Message not consumed");
        await sagaHarness.AssertConsumed<OrderFry, FryStateMachine, FryState>("Message not consumed by saga");
        await sagaHarness.AssertCreated(orderLineId);
        await sagaHarness.AssertState(orderLineId, x => x.Completed, "Saga did not reach Completed");

        await harness.Stop(TestContext.Current.CancellationToken);
    }
    
    private static ServiceProvider CreateProvider() =>
        new ServiceCollection()
            .AddForkJointEnterpriseServices()
            .ConfigureMassTransit(x =>
            {
                x.AddForkJointEnterpriseComponents();
            })
            .BuildServiceProvider(true);
}