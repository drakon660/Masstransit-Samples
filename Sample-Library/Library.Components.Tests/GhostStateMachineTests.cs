using AwesomeAssertions;
using Library.Components.StateMachines.Ghost;
using Library.Contracts;
using MassTransit;
using MassTransit.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Library.Components.Tests;

public class GhostStateMachineTests
{
    [Fact]
    public async Task Should_Transition_To_Active_When_Started_First()
    {
        await using var provider = CreateProvider();
        var harness = provider.GetTestHarness();
        await harness.Start();

        var ghostId = Guid.NewGuid();

        await harness.Bus.Publish<GhostStarted>(new
        {
            GhostId = ghostId,
            InVar.Timestamp,
        }, TestContext.Current.CancellationToken);

        var sagaHarness = harness.GetSagaStateMachineHarness<GhostStateMachine, Ghost>();

        await sagaHarness.AssertConsumed<GhostStarted, GhostStateMachine, Ghost>(
            f => f.Context.Message.GhostId == ghostId, "GhostStarted not consumed");

        var instance = await sagaHarness.FirstCreated(x => x.GhostId == ghostId);
        instance.Should().NotBeNull();

        await sagaHarness.AssertState(instance.Saga.CorrelationId, x => x.Active, "Saga not in Active");
    }

    [Fact]
    public async Task Should_Stay_In_Initial_When_Pinged_First()
    {
        await using var provider = CreateProvider();
        var harness = provider.GetTestHarness();
        await harness.Start();

        var ghostId = Guid.NewGuid();

        await harness.Bus.Publish<GhostPinged>(new
        {
            GhostId = ghostId,
            InVar.Timestamp,
        }, TestContext.Current.CancellationToken);

        var sagaHarness = harness.GetSagaStateMachineHarness<GhostStateMachine, Ghost>();

        await sagaHarness.AssertConsumed<GhostPinged, GhostStateMachine, Ghost>(
            f => f.Context.Message.GhostId == ghostId, "GhostPinged not consumed");

        var instance = await sagaHarness.FirstCreated(x => x.GhostId == ghostId);
        instance.Should().NotBeNull();

        // Ghost row created but stuck in Initial — no Initially handler for GhostPinged.
        await sagaHarness.AssertState(instance.Saga.CorrelationId, x => x.Initial, "Saga not in Initial");
    }

    [Fact]
    public async Task Should_Recover_Stuck_Initial_When_Started_Arrives_Later()
    {
        await using var provider = CreateProvider();
        var harness = provider.GetTestHarness();
        await harness.Start();

        var ghostId = Guid.NewGuid();
        var sagaHarness = harness.GetSagaStateMachineHarness<GhostStateMachine, Ghost>();

        await harness.Bus.Publish<GhostPinged>(new
        {
            GhostId = ghostId,
            InVar.Timestamp,
        }, TestContext.Current.CancellationToken);

        await sagaHarness.AssertConsumed<GhostPinged, GhostStateMachine, Ghost>(
            f => f.Context.Message.GhostId == ghostId, "GhostPinged not consumed");

        var instance = await sagaHarness.FirstCreated(x => x.GhostId == ghostId);
        instance.Should().NotBeNull();

        await harness.Bus.Publish<GhostStarted>(new
        {
            GhostId = ghostId,
            InVar.Timestamp,
        }, TestContext.Current.CancellationToken);

        await sagaHarness.AssertConsumed<GhostStarted, GhostStateMachine, Ghost>(
            f => f.Context.Message.GhostId == ghostId, "GhostStarted not consumed");

        await sagaHarness.AssertState(instance.Saga.CorrelationId, x => x.Active, "Saga not in Active");
    }

    private static ServiceProvider CreateProvider() =>
        new ServiceCollection()
            .ConfigureMassTransit(x => { x.AddSagaStateMachine<GhostStateMachine, Ghost>(); })
            .BuildServiceProvider(true);
}
