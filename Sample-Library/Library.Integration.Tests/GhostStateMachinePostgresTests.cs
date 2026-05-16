using AwesomeAssertions;
using Library.Components.StateMachines.Ghost;
using Library.Contracts;
using MassTransit;
using MassTransit.Testing;

namespace Library.Integration.Tests;

[Collection(nameof(PostgresCollection))]
public class GhostStateMachinePostgresTests(PostgresFixture postgres, ITestOutputHelper testOutputHelper)
{
    private readonly PostgresFixture _postgres = postgres;
    private readonly ITestOutputHelper _testOutputHelper = testOutputHelper;

    [Fact]
    public async Task Should_Persist_Saga_To_Postgres()
    {
        await using var provider = await LibraryIntegrationTestConfigurationExtensions
            .CreateProvider(_postgres.ConnectionString, _testOutputHelper);

        var harness = provider.GetTestHarness();
        await harness.Start();

        var ghostId = Guid.NewGuid();

        await harness.Bus.Publish<GhostStarted>(new
        {
            GhostId = ghostId,
            InVar.Timestamp,
        }, TestContext.Current.CancellationToken);

        var sagaHarness = harness.GetSagaStateMachineHarness<GhostStateMachine, Ghost>();

        (await sagaHarness.Consumed.Any<GhostStarted>(TestContext.Current.CancellationToken))
            .Should().BeTrue();

        (await sagaHarness.Created.Any(x => x.GhostId == ghostId, TestContext.Current.CancellationToken))
            .Should().BeTrue();
    }
}
