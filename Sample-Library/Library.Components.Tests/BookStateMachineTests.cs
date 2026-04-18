using AwesomeAssertions;
using Library.Components.StateMachines;
using Library.Contracts;
using MassTransit;
using MassTransit.Testing;
using Meziantou.Extensions.Logging.Xunit.v3;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Library.Components.Tests;

public class BookStateMachineTests(ITestOutputHelper output)
{
    [Fact]
    public async Task Should_Create_A_Saga_Instance()
    {
        await using var provider = new ServiceCollection()
            .AddLogging(lb => lb
                .AddProvider(new XUnitLoggerProvider(output))
                .SetMinimumLevel(LogLevel.Trace))
            .ConfigureMassTransit(x =>
            {
                x.AddSagaStateMachine<BookStateMachine, Book>();
            })
            .BuildServiceProvider(true);
        
        var harness = provider.GetTestHarness();
        
        await harness.Start();
        
        var bookId = NewId.NextGuid();

        await harness.Bus.Publish<BookAdded>(new
        {
            BookId = bookId,
            Isbn = "0307969959",
            Title = "Neuromancer"
        }, TestContext.Current.CancellationToken);

        (await harness.Consumed.Any<BookAdded>(TestContext.Current.CancellationToken)).Should().BeTrue("Message not consumed");

        var sagaHarness = harness.GetSagaStateMachineHarness<BookStateMachine, Book>();

        (await sagaHarness.Consumed.Any<BookAdded>(TestContext.Current.CancellationToken)).Should().BeTrue("Message not consumed by saga");

        (await sagaHarness.Created.Any(x => x.CorrelationId == bookId, TestContext.Current.CancellationToken)).Should().BeTrue();

        var instance = sagaHarness.Created.ContainsInState(bookId, sagaHarness.StateMachine, sagaHarness.StateMachine.Available);
        instance.Should().NotBeNull("Saga instance not found");

        Guid? existsId = await sagaHarness.Exists(bookId, x => x.Available);
        existsId.Should().HaveValue("Saga did not exist");
    }
}
