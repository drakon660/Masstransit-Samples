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
        
        var (harness, sagaHarness) = await StartBookHarness(provider);
        
        var bookId = NewId.NextGuid();

        await PublishBookAdded(harness, bookId);

        await AssertConsumed<BookAdded>(harness, "Message not consumed");
        await AssertConsumedBySaga<BookAdded>(sagaHarness, "Message not consumed by saga");
        await AssertSagaCreated(sagaHarness, bookId);
        AssertSagaInstanceInState(sagaHarness, bookId, x => x.Available, "Saga instance not found");
        await AssertBookState(sagaHarness, bookId, x => x.Available, "Saga did not exist");
    }

    private static ServiceProvider CreateProvider() =>
        new ServiceCollection()
            .ConfigureMassTransit(x =>
            {
                x.AddSagaStateMachine<BookStateMachine, Book>();
            })
            .BuildServiceProvider(true);

    private static async Task<(ITestHarness Harness, ISagaStateMachineTestHarness<BookStateMachine, Book> SagaHarness)> StartBookHarness(
        ServiceProvider provider)
    {
        var harness = provider.GetTestHarness();
        await harness.Start();

        return (harness, harness.GetSagaStateMachineHarness<BookStateMachine, Book>());
    }

    private static Task PublishBookAdded(
        ITestHarness harness,
        Guid bookId,
        string isbn = "0307969959",
        string title = "Neuromancer") =>
        harness.Bus.Publish<BookAdded>(new
        {
            BookId = bookId,
            Isbn = isbn,
            Title = title,
        }, TestContext.Current.CancellationToken);

    private static async Task AssertConsumed<T>(
        ITestHarness harness,
        string because)
        where T : class
    {
        (await harness.Consumed.Any<T>(TestContext.Current.CancellationToken)).Should().BeTrue(because);
    }

    private static async Task AssertConsumedBySaga<T>(
        ISagaStateMachineTestHarness<BookStateMachine, Book> sagaHarness,
        string because)
        where T : class
    {
        (await sagaHarness.Consumed.Any<T>(TestContext.Current.CancellationToken)).Should().BeTrue(because);
    }

    private static async Task AssertSagaCreated(
        ISagaStateMachineTestHarness<BookStateMachine, Book> sagaHarness,
        Guid bookId)
    {
        (await sagaHarness.Created.Any(x => x.CorrelationId == bookId, TestContext.Current.CancellationToken))
            .Should().BeTrue();
    }

    private static void AssertSagaInstanceInState(
        ISagaStateMachineTestHarness<BookStateMachine, Book> sagaHarness,
        Guid bookId,
        Func<BookStateMachine, State> stateSelector,
        string because)
    {
        var instance = sagaHarness.Created.ContainsInState(bookId, sagaHarness.StateMachine, stateSelector);
        instance.Should().NotBeNull(because);
    }

    private static async Task AssertBookState(
        ISagaStateMachineTestHarness<BookStateMachine, Book> sagaHarness,
        Guid bookId,
        Func<BookStateMachine, State> stateSelector,
        string because)
    {
        Guid? existsId = await sagaHarness.Exists(bookId, stateSelector);
        existsId.Should().HaveValue(because);
    }
}
