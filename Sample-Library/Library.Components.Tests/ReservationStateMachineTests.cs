using AwesomeAssertions;
using Library.Components.StateMachines.Book;
using Library.Components.StateMachines.Reservation;
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

        await harness.PublishReservationRequested(reservationId, Guid.NewGuid(), bookId);

        await harness.AssertConsumed<ReservationRequested>("Message not consumed");

        var sagaHarness = harness.GetSagaStateMachineHarness<ReservationStateMachine, Reservation>();

        await sagaHarness.AssertConsumed<ReservationRequested, ReservationStateMachine, Reservation>("Message not consumed by saga");
        await sagaHarness.AssertCreated(reservationId);

        var instance = sagaHarness.Created.ContainsInState(reservationId, sagaHarness.StateMachine, sagaHarness.StateMachine.Requested);
        instance.Should().NotBeNull("Saga instance not found");

        await sagaHarness.AssertState(reservationId, x => x.Requested, "Saga did not exist");
    }

    [Fact]
    public async Task Should_Reserve_A_Book()
    {
        var reservationId = NewId.NextGuid();
        var bookId = NewId.NextGuid();
        var memberId = NewId.NextGuid();

        await using var context = await CreateABook(bookId);

        await context.Harness.PublishReservationRequested(reservationId, memberId, bookId);

        await context.ReservationSagaHarness.AssertConsumed<ReservationRequested, ReservationStateMachine, Reservation>("Message not consumed by saga");
        await context.BookSagaHarness.AssertConsumed<ReservationRequested, BookStateMachine, Book>("Message not consumed by saga");

        await context.ReservationSagaHarness.AssertState(reservationId, x => x.Reserved, "Saga instance not found");
        await context.BookSagaHarness.AssertState(bookId, x => x.Reserved, "Saga instance not found");
    }

    [Fact]
    public async Task When_Reservation_Expires_Should_Mark_Book_As_Available()
    {
        var reservationId = NewId.NextGuid();
        var bookId = NewId.NextGuid();
        var memberId = NewId.NextGuid();

        await using var context = await CreateABook(bookId);

        await context.Harness.PublishReservationRequested(reservationId, memberId, bookId);

        await context.ReservationSagaHarness.AssertState(reservationId, x => x.Reserved, "Saga instance not found");
        await context.BookSagaHarness.AssertState(bookId, x => x.Reserved, "Saga instance not found");

        using var adjustment = new QuartzTimeAdjustment(context.Provider);

        await adjustment.AdvanceTime(TimeSpan.FromHours(24));

        await context.ReservationSagaHarness.AssertConsumed<ReservationExpired, ReservationStateMachine, Reservation>("Reservation saga should have consumed expiration");
        await context.ReservationSagaHarness.AssertNotExists(reservationId);
        await context.BookSagaHarness.AssertState(bookId, x => x.Available, "Saga instance not found");
    }

    [Fact]
    public async Task When_Reservation_Expires_With_Custom_Duration_Should_Mark_Book_As_Available()
    {
        var reservationId = NewId.NextGuid();
        var bookId = NewId.NextGuid();
        var memberId = NewId.NextGuid();

        await using var context = await CreateABook(bookId);

        await context.Harness.PublishReservationRequested(reservationId, memberId, bookId, TimeSpan.FromDays(2));

        await context.ReservationSagaHarness.AssertState(reservationId, x => x.Reserved, "Reservation was not reserved");
        await context.BookSagaHarness.AssertState(bookId, x => x.Reserved, "Saga instance not found");

        using var adjustment = new QuartzTimeAdjustment(context.Provider);

        await adjustment.AdvanceTime(TimeSpan.FromHours(24));
        await context.ReservationSagaHarness.AssertState(
            reservationId,
            x => x.Reserved,
            "Reservation should still be reserved before the two day duration elapses");

        await adjustment.AdvanceTime(TimeSpan.FromHours(24));
        await AssertReservationCancelled(context.Harness, context.BookSagaHarness);
        await context.ReservationSagaHarness.AssertNotExists(reservationId);
        await context.BookSagaHarness.AssertState(bookId, x => x.Available, "Saga instance not found");
    }

    [Fact]
    public async Task When_Book_Is_Checked_Out_Reservation_Should_Be_Removed()
    {
        var reservationId = NewId.NextGuid();
        var bookId = NewId.NextGuid();
        var memberId = NewId.NextGuid();

        await using var context = await CreateABook(bookId);

        await context.Harness.PublishReservationRequested(reservationId, memberId, bookId, TimeSpan.FromDays(2));

        await context.ReservationSagaHarness.AssertState(reservationId, x => x.Reserved, "Reservation was not reserved");
        await context.BookSagaHarness.AssertState(bookId, x => x.Reserved, "Saga instance not found");

        await context.Harness.PublishBookCheckedOut(bookId, memberId);

        await context.ReservationSagaHarness.AssertConsumed<BookCheckedOut, ReservationStateMachine, Reservation>("Reservation saga should have consumed the checkout message");
        await context.BookSagaHarness.AssertState(bookId, x => x.CheckedOut, "Saga instance not found");
        await context.ReservationSagaHarness.AssertNotExists(reservationId);
    }

    [Fact]
    public async Task When_Reservation_Cancelled_Should_Mark_Book_As_Available()
    {
        var reservationId = NewId.NextGuid();
        var bookId = NewId.NextGuid();
        var memberId = NewId.NextGuid();

        await using var context = await CreateABook(bookId);

        await context.Harness.PublishReservationRequested(reservationId, memberId, bookId, TimeSpan.FromDays(2));

        await context.ReservationSagaHarness.AssertState(reservationId, x => x.Reserved, "Reservation was not reserved");
        await context.BookSagaHarness.AssertState(bookId, x => x.Reserved, "Saga instance not found");

        await context.Harness.PublishReservationCancellationRequested(reservationId);

        await AssertReservationCancelled(context.Harness, context.BookSagaHarness);
        await context.ReservationSagaHarness.AssertNotExists(reservationId);
        await context.BookSagaHarness.AssertState(bookId, x => x.Available, "Saga instance not found");
    }

    [Fact]
    public async Task When_Reservation_For_Already_Reserved_Book_Is_Requested_Should_Not_Reserve_The_Book()
    {
        var reservationId = NewId.NextGuid();
        var bookId = NewId.NextGuid();
        var memberId = NewId.NextGuid();

        await using var context = await CreateABook(bookId);

        await context.Harness.PublishReservationRequested(reservationId, memberId, bookId, TimeSpan.FromDays(2));
        await context.ReservationSagaHarness.AssertState(reservationId, x => x.Reserved, "Reservation was not reserved");

        var secondReservationId = NewId.NextGuid();
        await context.Harness.PublishReservationRequested(secondReservationId, memberId, bookId, TimeSpan.FromDays(2));

        await context.ReservationSagaHarness.AssertState(secondReservationId, x => x.Requested, "Reservation was not in Requested state");
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

    private static async Task AssertReservationCancelled(
        ITestHarness harness,
        ISagaStateMachineTestHarness<BookStateMachine, Book> bookSagaHarness)
    {
        await harness.AssertPublished<BookReservationCancelled>("Reservation cancellation should have been published");
        await bookSagaHarness.AssertConsumed<BookReservationCancelled, BookStateMachine, Book>("Book saga should have consumed the reservation cancellation message");
    }

    private static async Task<BookTestContext> CreateABook(Guid bookId)
    {
        var provider = CreateProvider(includeBookStateMachine: true);

        var harness = provider.GetTestHarness();

        await harness.Start();

        var reservationSagaHarness = harness.GetSagaStateMachineHarness<ReservationStateMachine, Reservation>();
        var bookSagaHarness = harness.GetSagaStateMachineHarness<BookStateMachine, Book>();

        await harness.PublishBookAdded(bookId);

        await bookSagaHarness.AssertState(bookId, x => x.Available, "Saga instance not found");

        return new BookTestContext(provider, harness, reservationSagaHarness, bookSagaHarness);
    }

    private sealed class BookTestContext(
        ServiceProvider provider,
        ITestHarness harness,
        ISagaStateMachineTestHarness<ReservationStateMachine, Reservation> reservationSagaHarness,
        ISagaStateMachineTestHarness<BookStateMachine, Book> bookSagaHarness)
        : IAsyncDisposable
    {
        public ServiceProvider Provider { get; } = provider;
        public ITestHarness Harness { get; } = harness;
        public ISagaStateMachineTestHarness<ReservationStateMachine, Reservation> ReservationSagaHarness { get; } = reservationSagaHarness;
        public ISagaStateMachineTestHarness<BookStateMachine, Book> BookSagaHarness { get; } = bookSagaHarness;

        public ValueTask DisposeAsync() => Provider.DisposeAsync();
    }
}
