using System.ComponentModel;
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
        var reservationId = NewId.NextGuid();
        var bookId = NewId.NextGuid();
        var memberId = NewId.NextGuid();

        await using var context = await CreateABook(bookId);
        
        await PublishReservationRequested(context.Harness, reservationId, memberId, bookId);

        await AssertConsumedByReservationSaga<ReservationRequested>(context.ReservationSagaHarness, "Message not consumed by saga");
        await AssertConsumedByBookSaga<ReservationRequested>(context.BookSagaHarness, "Message not consumed by saga");
        
        await AssertReservationState(context.ReservationSagaHarness, reservationId, x => x.Reserved, "Saga instance not found");
        await AssertBookState(context.BookSagaHarness, bookId, x => x.Reserved, "Saga instance not found");
    }
    
    [Fact]
    public async Task When_Reservation_Expires_Should_Mark_Book_As_Available()
    {
        var reservationId = NewId.NextGuid();
        var bookId = NewId.NextGuid();
        var memberId = NewId.NextGuid();

        await using var context = await CreateABook(bookId);
        
        await PublishReservationRequested(context.Harness, reservationId, memberId, bookId);
        
        await AssertReservationState(context.ReservationSagaHarness, reservationId, x => x.Requested, "Saga instance not found");
        
        await AssertBookState(context.BookSagaHarness, bookId, x => x.Reserved, "Saga instance not found");
        
        using var adjustment = new QuartzTimeAdjustment(context.Provider);
        
        await AdvanceTime(adjustment, TimeSpan.FromHours(24));

        await AssertConsumedByReservationSaga<ReservationExpired>(context.ReservationSagaHarness, "dupa");
        await AssertReservationRemoved(context.ReservationSagaHarness, reservationId);
        await AssertBookState(context.BookSagaHarness, bookId, x => x.Available, "Saga instance not found");
    }
    
    [Fact]
    public async Task When_Reservation_Expires_With_Custom_Duration_Should_Mark_Book_As_Available()
    {
        var reservationId = NewId.NextGuid();
        var bookId = NewId.NextGuid();
        var memberId = NewId.NextGuid();

        await using var context = await CreateABook(bookId);
        
        await PublishReservationRequested(context.Harness, reservationId, memberId, bookId, TimeSpan.FromDays(2));

        await AssertReservationState(context.ReservationSagaHarness, reservationId, x => x.Reserved, "Reservation was not reserved");
        await AssertBookState(context.BookSagaHarness, bookId, x => x.Reserved, "Saga instance not found");

        using var adjustment = new QuartzTimeAdjustment(context.Provider);

        await AdvanceTime(adjustment, TimeSpan.FromHours(24));
        await AssertReservationState(
            context.ReservationSagaHarness,
            reservationId,
            x => x.Reserved,
            "Reservation should still be reserved before the two day duration elapses");

        await AdvanceTime(adjustment, TimeSpan.FromHours(24));
        await AssertReservationCancelled(context.Harness, context.BookSagaHarness);
        await AssertReservationRemoved(context.ReservationSagaHarness, reservationId);
        await AssertBookState(context.BookSagaHarness, bookId, x => x.Available, "Saga instance not found");
    }

    [Fact]
    public async Task When_Book_Is_Checked_Out_Reservation_Should_Be_Removed()
    {
        var reservationId = NewId.NextGuid();
        var bookId = NewId.NextGuid();
        var memberId = NewId.NextGuid();

        await using var context = await CreateABook(bookId);
        
        await PublishReservationRequested(context.Harness, reservationId, memberId, bookId, TimeSpan.FromDays(2));

        await AssertReservationState(context.ReservationSagaHarness, reservationId, x => x.Reserved, "Reservation was not reserved");
        await AssertBookState(context.BookSagaHarness, bookId, x => x.Reserved, "Saga instance not found");

        await context.Harness.Bus.Publish<BookCheckedOut>(new
        {
            BookId = bookId,
            InVar.Timestamp,
            MemberId = memberId,
        }, TestContext.Current.CancellationToken);
        
        await AssertConsumedByReservationSaga<BookCheckedOut>(context.ReservationSagaHarness, "Reservation saga should have consumed the checkout message");
        await AssertBookState(context.BookSagaHarness, bookId, x => x.CheckedOut, "Saga instance not found");
        await AssertReservationRemoved(context.ReservationSagaHarness, reservationId);
    }

    [Fact]
    public async Task When_Reservation_For_Already_Reserved_Book_Is_Requested_Should_Not_Reserve_The_Book()
    {
        var reservationId = NewId.NextGuid();
        var bookId = NewId.NextGuid();
        var memberId = NewId.NextGuid();

        await using var context = await CreateABook(bookId);
        
        await PublishReservationRequested(context.Harness, reservationId, memberId, bookId, TimeSpan.FromDays(2));
        await AssertReservationState(context.ReservationSagaHarness, reservationId, x => x.Reserved, "Reservation was not reserved");
        
        var secondReservationId = NewId.NextGuid();
        await PublishReservationRequested(context.Harness, secondReservationId, memberId, bookId, TimeSpan.FromDays(2));
        
        await AssertReservationState(context.ReservationSagaHarness, secondReservationId, x => x.Requested, "Reservation was not reserved");
    }
    
    [Fact]
    public async Task When_Reservation_Cancelled_Should_Mark_Book_As_Available()
    {
        var reservationId = NewId.NextGuid();
        var bookId = NewId.NextGuid();
        var memberId = NewId.NextGuid();

        await using var context = await CreateABook(bookId);
        
        await PublishReservationRequested(context.Harness, reservationId, memberId, bookId, TimeSpan.FromDays(2));

        await AssertReservationState(context.ReservationSagaHarness, reservationId, x => x.Reserved, "Reservation was not reserved");
        await AssertBookState(context.BookSagaHarness, bookId, x => x.Reserved, "Saga instance not found");
        
        await context.Harness.Bus.Publish<ReservationCancellationRequested>(new
        {
            ReservationId = reservationId,
            InVar.Timestamp,
        }, TestContext.Current.CancellationToken);

        await AssertReservationCancelled(context.Harness, context.BookSagaHarness);
        await AssertReservationRemoved(context.ReservationSagaHarness, reservationId);
        await AssertBookState(context.BookSagaHarness, bookId, x => x.Available, "Saga instance not found");
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
        => await AssertStateMachine(reservationSagaHarness, reservationId, stateSelector, because);

    private static async Task AssertBookState(
        ISagaStateMachineTestHarness<BookStateMachine, Book> bookSagaHarness,
        Guid bookId,
        Func<BookStateMachine, State> stateSelector,
        string because)
        => await AssertStateMachine(bookSagaHarness, bookId, stateSelector, because);
    
    private static async Task AssertStateMachine<TStateMachine, TInstance>(
        ISagaStateMachineTestHarness<TStateMachine, TInstance> sagaHarness,
        Guid correlationId,
        Func<TStateMachine, State> stateSelector,
        string because) 
        where TStateMachine : SagaStateMachine<TInstance>
        where TInstance : class, SagaStateMachineInstance
    {
        var existsId = await sagaHarness.Exists(correlationId, stateSelector);
        existsId.Should().NotBeEmpty(because);
    }

    private static async Task<BookTestContext> CreateABook(Guid bookId)
    {
        var provider = CreateProvider(includeBookStateMachine: true);

        var harness = provider.GetTestHarness();

        await harness.Start();
        
        var reservationSagaHarness = harness.GetSagaStateMachineHarness<ReservationStateMachine, Reservation>();
        var bookSagaHarness = harness.GetSagaStateMachineHarness<BookStateMachine, Book>();
        
        await harness.Bus.Publish<BookAdded>(new
        {
            BookId = bookId,
            Isbn = "0307969959",
            Title = "Neuromancer",
        }, TestContext.Current.CancellationToken);
        
        await AssertBookState(bookSagaHarness, bookId, x => x.Available, "Saga instance not found");

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
