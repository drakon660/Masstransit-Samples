using AwesomeAssertions;
using Library.Components.StateMachines;
using Library.Contracts;
using MassTransit;
using MassTransit.Testing;
using Meziantou.Extensions.Logging.Xunit.v3;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Library.Components.Tests;

public class ReservationStateMachineTests(ITestOutputHelper output)
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
                x.AddSagaStateMachine<ReservationStateMachine, Reservation>();
            })
            .BuildServiceProvider(true);
        
        var harness = provider.GetTestHarness();
        
        await harness.Start();
        
        var bookId = Guid.NewGuid();
        var reservationId = Guid.NewGuid();
        
        await harness.Bus.Publish<ReservationRequested>(new
        {
            BookId = bookId,
            ReservationId = reservationId,
            Isbn = "0307969959",
            Title = "Neuromancer"
        }, TestContext.Current.CancellationToken);

        (await harness.Consumed.Any<ReservationRequested>(TestContext.Current.CancellationToken)).Should().BeTrue("Message not consumed");

        var sagaHarness = harness.GetSagaStateMachineHarness<ReservationStateMachine, Reservation>();

        (await sagaHarness.Consumed.Any<ReservationRequested>(TestContext.Current.CancellationToken)).Should().BeTrue("Message not consumed by saga");

        (await sagaHarness.Created.Any(x => x.CorrelationId == reservationId, TestContext.Current.CancellationToken)).Should().BeTrue();

        var instance = sagaHarness.Created.ContainsInState(reservationId, sagaHarness.StateMachine, sagaHarness.StateMachine.Requested);
        instance.Should().NotBeNull("Saga instance not found");

        Guid? existsId = await sagaHarness.Exists(reservationId, x => x.Requested);
        existsId.Should().HaveValue("Saga did not exist");
    }

    [Fact]
    public async Task Should_Reserve_A_Book()
    {
        await using var provider = new ServiceCollection()
            .ConfigureMassTransit(x =>
            {
                x.AddSagaStateMachine<ReservationStateMachine, Reservation>();
                x.AddSagaStateMachine<BookStateMachine, Book>();
            })
            .BuildServiceProvider(true);

        var harness = provider.GetTestHarness();

        await harness.Start();
        
        var reservationSagaHarness = harness.GetSagaStateMachineHarness<ReservationStateMachine, Reservation>();
        var bookSagaHarness = harness.GetSagaStateMachineHarness<BookStateMachine, Book>();
        
        var reservationId = NewId.NextGuid();
        var bookId = NewId.NextGuid();
        var memberId = NewId.NextGuid();

        await harness.Bus.Publish<BookAdded>(new
        {
            BookId = bookId,
            Isbn = "0307969959",
            Title = "Neuromancer"
        }, TestContext.Current.CancellationToken);
        
        Guid? existsId = await bookSagaHarness.Exists(bookId, x => x.Available);
        existsId.Should().NotBeEmpty("Saga instance not found");
        
        await harness.Bus.Publish<ReservationRequested>(new
        {
            ReservationId = reservationId,
            InVar.Timestamp,
            MemberId = memberId,
            BookId = bookId,
        }, TestContext.Current.CancellationToken);

        (await reservationSagaHarness.Consumed.Any<ReservationRequested>(TestContext.Current.CancellationToken))
            .Should().BeTrue("Message not consumed by saga");
        
        (await bookSagaHarness.Consumed.Any<ReservationRequested>(TestContext.Current.CancellationToken))
            .Should().BeTrue("Message not consumed by saga");
        
        existsId = await reservationSagaHarness.Exists(reservationId, x => x.Requested);
        existsId.Should().NotBeEmpty("Saga instance not found");

        existsId = await bookSagaHarness.Exists(bookId, x => x.Reserved);
        existsId.Should().NotBeEmpty("Saga instance not found");
    }
}