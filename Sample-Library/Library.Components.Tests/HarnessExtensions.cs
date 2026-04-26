using AwesomeAssertions;
using Library.Contracts;
using MassTransit;
using MassTransit.Testing;

namespace Library.Components.Tests;

public static class HarnessExtensions
{
    public static Task PublishBookAdded(
        this ITestHarness harness,
        Guid bookId,
        string isbn = "0307969959",
        string title = "Neuromancer") =>
        harness.Bus.Publish<BookAdded>(new
        {
            BookId = bookId,
            Isbn = isbn,
            Title = title,
            InVar.Timestamp,
        }, TestContext.Current.CancellationToken);

    public static Task PublishReservationRequested(
        this ITestHarness harness,
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

    public static Task PublishBookCheckedOut(
        this ITestHarness harness,
        Guid bookId,
        Guid memberId,
        Guid? checkOutId = null) =>
        harness.Bus.Publish<BookCheckedOut>(new
        {
            CheckOutId = checkOutId ?? NewId.NextGuid(),
            BookId = bookId,
            MemberId = memberId,
            InVar.Timestamp,
        }, TestContext.Current.CancellationToken);

  public static Task PublishReservationCancellationRequested(
        this ITestHarness harness,
        Guid reservationId) =>
        harness.Bus.Publish<ReservationCancellationRequested>(new
        {
            ReservationId = reservationId,
            InVar.Timestamp,
        }, TestContext.Current.CancellationToken);

    public static async Task AssertConsumed<T>(this ITestHarness harness, string because)
        where T : class =>
        (await harness.Consumed.Any<T>(TestContext.Current.CancellationToken))
            .Should().BeTrue(because);

    public static async Task AssertPublished<T>(this ITestHarness harness, string because)
        where T : class =>
        (await harness.Published.Any<T>(TestContext.Current.CancellationToken))
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
        where TInstance : class, SagaStateMachineInstance =>
        (await sagaHarness.Created.Any(x => x.CorrelationId == correlationId, TestContext.Current.CancellationToken))
            .Should().BeTrue();

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

    public static async Task AssertNotExists<TStateMachine, TInstance>(
        this ISagaStateMachineTestHarness<TStateMachine, TInstance> sagaHarness,
        Guid correlationId)
        where TStateMachine : SagaStateMachine<TInstance>
        where TInstance : class, SagaStateMachineInstance
    {
        var notExists = await sagaHarness.NotExists(correlationId);
        notExists.Should().BeNull();
    }
}
