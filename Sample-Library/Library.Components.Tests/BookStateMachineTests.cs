using AwesomeAssertions;
using Library.Components.StateMachines;
using Library.Contracts;
using MassTransit;
using MassTransit.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Library.Components.Tests;

public class BookStateMachineTests
{
    [Fact]
    public async Task Should_Create_A_Saga_Instance()
    {
        await using var provider = CreateProvider();

        var harness = provider.GetTestHarness();
        await harness.Start();

        var sagaHarness = harness.GetSagaStateMachineHarness<BookStateMachine, Book>();

        var bookId = NewId.NextGuid();

        await harness.PublishBookAdded(bookId);

        await harness.AssertConsumed<BookAdded>("Message not consumed");
        await sagaHarness.AssertConsumed<BookAdded, BookStateMachine, Book>("Message not consumed by saga");
        await sagaHarness.AssertCreated(bookId);

        var instance = sagaHarness.Created.ContainsInState(bookId, sagaHarness.StateMachine, sagaHarness.StateMachine.Available);
        instance.Should().NotBeNull("Saga instance not found");

        await sagaHarness.AssertState(bookId, x => x.Available, "Saga did not exist");
    }

    private static ServiceProvider CreateProvider() =>
        new ServiceCollection()
            .ConfigureMassTransit(x =>
            {
                x.AddSagaStateMachine<BookStateMachine, Book>();
            })
            .BuildServiceProvider(true);
}
