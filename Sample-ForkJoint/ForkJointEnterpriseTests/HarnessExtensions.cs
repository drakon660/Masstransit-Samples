using AwesomeAssertions;
using ForkJointEnterprise.Contracts;
using MassTransit;
using MassTransit.Testing;

namespace ForkJointEnterpriseTests;

public static class HarnessExtensions
{
    public static async Task AssertConsumed<T>(this ITestHarness harness, string because)
        where T : class =>
        (await harness.Consumed.Any<T>(TestContext.Current.CancellationToken))
        .Should().BeTrue(because);
    
    public static async Task AssertConsumed<T, TStateMachine, TInstance>(
        this ISagaStateMachineTestHarness<TStateMachine, TInstance> sagaHarness,
        string because)
        where T : class
        where TStateMachine : SagaStateMachine<TInstance>
        where TInstance : class, SagaStateMachineInstance =>
        (await sagaHarness.Consumed.Any<T>(TestContext.Current.CancellationToken))
        .Should().BeTrue(because);
    
    public static async Task AssertCreated<TStateMachine, TInstance>(
        this ISagaStateMachineTestHarness<TStateMachine, TInstance> sagaHarness,
        Guid correlationId)
        where TStateMachine : SagaStateMachine<TInstance>
        where TInstance : class, SagaStateMachineInstance
    {
        (await sagaHarness.Created.Any(x => x.CorrelationId == correlationId,
                TestContext.Current.CancellationToken))
            .Should().BeTrue();
    }
    
    public static async Task AssertState<TStateMachine, TInstance>(
        this ISagaStateMachineTestHarness<TStateMachine, TInstance> sagaHarness,
        Guid correlationId,
        Func<TStateMachine, State> stateSelector,
        string because)
        where TStateMachine : SagaStateMachine<TInstance>
        where TInstance : class, SagaStateMachineInstance
    {
        var existsId = await sagaHarness.Exists(correlationId, stateSelector);
        existsId.Should().NotBeEmpty(because);
    }
}