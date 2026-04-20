using AwesomeAssertions;
using Library.Components.StateMachines;
using Library.Contracts;
using MassTransit;
using MassTransit.QuartzIntegration;
using MassTransit.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Library.Components.Tests;

public class ReservationStateMachineTests
{
    [Fact]
    public async Task Should_Create_A_Saga_Instance()
    {
        await using var provider = CreateProvider();
        
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

        await AssertConsumed<ReservationRequested>(harness, "Message not consumed");

        var sagaHarness = harness.GetSagaStateMachineHarness<ReservationStateMachine, Reservation>();

        await AssertConsumedByReservationSaga<ReservationRequested>(sagaHarness, "Message not consumed by saga");
        await AssertReservationCreated(sagaHarness, reservationId);

        var instance = sagaHarness.Created.ContainsInState(reservationId, sagaHarness.StateMachine, sagaHarness.StateMachine.Requested);
        instance.Should().NotBeNull("Saga instance not found");

        Guid? existsId = await sagaHarness.Exists(reservationId, x => x.Requested);
        existsId.Should().HaveValue("Saga did not exist");
    }

    [Fact]
    public async Task Should_Reserve_A_Book()
    {
        await using var provider = CreateProvider(includeBookStateMachine: true);

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
        
        await AssertBookState(bookSagaHarness, bookId, x => x.Available, "Saga instance not found");
        
        await PublishReservationRequested(harness, reservationId, memberId, bookId);

        await AssertConsumedByReservationSaga<ReservationRequested>(reservationSagaHarness, "Message not consumed by saga");
        await AssertConsumedByBookSaga<ReservationRequested>(bookSagaHarness, "Message not consumed by saga");
        
        await AssertReservationState(reservationSagaHarness, reservationId, x => x.Reserved, "Saga instance not found");
        await AssertBookState(bookSagaHarness, bookId, x => x.Reserved, "Saga instance not found");
    }
    
    [Fact]
    public async Task When_Reservation_Expires_Should_Mark_Book_As_Available()
    {
        await using var provider = CreateProvider(includeBookStateMachine: true);

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
        
        await AssertBookState(bookSagaHarness, bookId, x => x.Available, "Saga instance not found");
        
        await PublishReservationRequested(harness, reservationId, memberId, bookId);
        
        await AssertReservationState(reservationSagaHarness, reservationId, x => x.Requested, "Saga instance not found");
        
        await AssertBookState(bookSagaHarness, bookId, x => x.Reserved, "Saga instance not found");
        
        using var adjustment = new QuartzTimeAdjustment(provider);
        
        await AdvanceTime(adjustment, TimeSpan.FromHours(24));

        await AssertConsumedByReservationSaga<ReservationExpired>(reservationSagaHarness, "dupa");
        await AssertReservationRemoved(reservationSagaHarness, reservationId);
        await AssertBookState(bookSagaHarness, bookId, x => x.Available, "Saga instance not found");
    }
    
    [Fact]
    public async Task When_Reservation_Expires_With_Custom_Duration_Should_Mark_Book_As_Available()
    {
        await using var provider = CreateProvider(includeBookStateMachine: true);

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
            Title = "Neuromancer",
        }, TestContext.Current.CancellationToken);
        
        await AssertBookState(bookSagaHarness, bookId, x => x.Available, "Saga instance not found");
        
        await PublishReservationRequested(harness, reservationId, memberId, bookId, TimeSpan.FromDays(2));

        await AssertReservationState(reservationSagaHarness, reservationId, x => x.Reserved, "Reservation was not reserved");
        await AssertBookState(bookSagaHarness, bookId, x => x.Reserved, "Saga instance not found");

        using var adjustment = new QuartzTimeAdjustment(provider);

        await AdvanceTime(adjustment, TimeSpan.FromHours(24));
        await AssertReservationState(
            reservationSagaHarness,
            reservationId,
            x => x.Reserved,
            "Reservation should still be reserved before the two day duration elapses");

        await AdvanceTime(adjustment, TimeSpan.FromHours(24));
        await AssertReservationCancelled(harness, bookSagaHarness);
        await AssertReservationRemoved(reservationSagaHarness, reservationId);
        await AssertBookState(bookSagaHarness, bookId, x => x.Available, "Saga instance not found");
    }

    private static ServiceProvider CreateProvider(bool includeBookStateMachine = false) =>
        new ServiceCollection()
            .ConfigureMassTransit(x =>
            {
                x.AddSagaStateMachine<ReservationStateMachine, Reservation>();

                if (includeBookStateMachine)
                    x.AddSagaStateMachine<BookStateMachine, Book>();
            })
            .BuildServiceProvider(true);

    private static Task AdvanceTime(QuartzTimeAdjustment adjustment, TimeSpan duration) =>
        adjustment.AdvanceTime(duration);

    private static async Task AssertConsumed<T>(
        ITestHarness harness,
        string because)
        where T : class
    {
        (await harness.Consumed.Any<T>(TestContext.Current.CancellationToken)).Should().BeTrue(because);
    }

    private static async Task AssertConsumedByReservationSaga<T>(
        ISagaStateMachineTestHarness<ReservationStateMachine, Reservation> reservationSagaHarness,
        string because)
        where T : class
    {
        (await reservationSagaHarness.Consumed.Any<T>(TestContext.Current.CancellationToken)).Should().BeTrue(because);
    }

    private static async Task AssertConsumedByBookSaga<T>(
        ISagaStateMachineTestHarness<BookStateMachine, Book> bookSagaHarness,
        string because)
        where T : class
    {
        (await bookSagaHarness.Consumed.Any<T>(TestContext.Current.CancellationToken)).Should().BeTrue(because);
    }

    private static async Task AssertReservationCreated(
        ISagaStateMachineTestHarness<ReservationStateMachine, Reservation> reservationSagaHarness,
        Guid reservationId)
    {
        (await reservationSagaHarness.Created.Any(x => x.CorrelationId == reservationId, TestContext.Current.CancellationToken))
            .Should().BeTrue();
    }

    private static Task PublishReservationRequested(
        ITestHarness harness,
        Guid reservationId,
        Guid memberId,
        Guid bookId,
        TimeSpan? duration = null) =>
        harness.Bus.Publish<ReservationRequested>(new
        {
            ReservationId = reservationId,
            InVar.Timestamp,
            Duration = duration,
            MemberId = memberId,
            BookId = bookId,
        }, TestContext.Current.CancellationToken);

    private static async Task AssertReservationCancelled(
        ITestHarness harness,
        ISagaStateMachineTestHarness<BookStateMachine, Book> bookSagaHarness)
    {
        (await harness.Published.Any<BookReservationCancelled>(TestContext.Current.CancellationToken))
            .Should().BeTrue("Reservation cancellation should have been published");

        (await bookSagaHarness.Consumed.Any<BookReservationCancelled>(TestContext.Current.CancellationToken))
            .Should().BeTrue("Book saga should have consumed the reservation cancellation message");
    }

    private static async Task AssertReservationRemoved(
        ISagaStateMachineTestHarness<ReservationStateMachine, Reservation> reservationSagaHarness,
        Guid reservationId)
    {
        var notExists = await reservationSagaHarness.NotExists(reservationId);
        notExists.Should().BeNull();
    }

    private static async Task AssertReservationState(
        ISagaStateMachineTestHarness<ReservationStateMachine, Reservation> reservationSagaHarness,
        Guid reservationId,
        Func<ReservationStateMachine, State> stateSelector,
        string because)
    {
        var existsId = await reservationSagaHarness.Exists(reservationId, stateSelector);
        existsId.Should().NotBeEmpty(because);
    }

    private static async Task AssertBookState(
        ISagaStateMachineTestHarness<BookStateMachine, Book> bookSagaHarness,
        Guid bookId,
        Func<BookStateMachine, State> stateSelector,
        string because)
    {
        var existsId = await bookSagaHarness.Exists(bookId, stateSelector);
        existsId.Should().NotBeEmpty(because);
    }
}
