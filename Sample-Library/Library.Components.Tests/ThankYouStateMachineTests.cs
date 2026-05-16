using AwesomeAssertions;
using Library.Components.StateMachines.Book;
using Library.Components.StateMachines.Reservation;
using Library.Components.StateMachines.ThankYou;
using Library.Contracts;
using MassTransit;
using MassTransit.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Library.Components.Tests;

public class ThankYouStateMachineTests
{
    public ThankYouStateMachineTests(ITestOutputHelper testOutputHelper)
    {
        Library.Components.Tests.Xunit.TestOutputRelay.Use(testOutputHelper);
    }

    [Fact]
    public async Task Should_Create_A_Saga_Instance_In_Order()
    {
        await using var provider = CreateProvider();

        var harness = provider.GetTestHarness();

        await harness.Start();

        var bookId = Guid.NewGuid();
        var reservationId = Guid.NewGuid();
        var memberId = Guid.NewGuid();

        await harness.Bus.Publish<BookReserved>(new
        {
            ReservationId = reservationId,
            MemberId = memberId,
            bookId,
            Duration = TimeSpan.FromDays(14),
            Timestamp = InVar.Timestamp
        }, TestContext.Current.CancellationToken);

        await harness.AssertConsumed<BookReserved>("Message not consumed");

        var sagaHarness = harness.GetSagaStateMachineHarness<ThankYouStateMachine, ThankYou>();

        await sagaHarness.AssertConsumed<BookReserved, ThankYouStateMachine, ThankYou>(
            filter => filter.Context.Message.BookId == bookId, "Message not consumed by saga");

        await sagaHarness.AssertCreated();

        await harness.Bus.Publish<BookCheckedOut>(new
        {
            CheckoutId = InVar.CorrelationId,
            MemberId = memberId,
            bookId,
            InVar.Timestamp
        }, TestContext.Current.CancellationToken);

        await sagaHarness
            .AssertConsumed<BookCheckedOut, ThankYouStateMachine, ThankYou>(filter=> filter.Context.Message.BookId == bookId,"Message not consumed by saga");

        var instance = await sagaHarness.FirstCreated(x => x.BookId == bookId && x.MemberId == memberId);

        instance.Should().NotBeNull();

        await sagaHarness.AssertState(instance.Saga.CorrelationId, x => x.Ready, "Saga did not transition to ready");
    }
    
    [Fact]
    public async Task Should_Create_A_Saga_Instance_Out_Of_Order()
    {
        await using var provider = CreateProvider();

        var harness = provider.GetTestHarness();

        await harness.Start();

        var bookId = Guid.NewGuid();
        var reservationId = Guid.NewGuid();
        var memberId = Guid.NewGuid();
        
        var sagaHarness = harness.GetSagaStateMachineHarness<ThankYouStateMachine, ThankYou>();
        
        await harness.Bus.Publish<BookCheckedOut>(new
        {
            CheckoutId = InVar.CorrelationId,
            MemberId = memberId,
            bookId,
            InVar.Timestamp
        }, TestContext.Current.CancellationToken);

        await sagaHarness
            .AssertConsumed<BookCheckedOut, ThankYouStateMachine, ThankYou>(filter=> filter.Context.Message.BookId == bookId,"Message not consumed by saga");
        
        var instance = await sagaHarness.FirstCreated(x => x.BookId == bookId && x.MemberId == memberId);
        
        instance.Should().NotBeNull();
        
        await sagaHarness.AssertState(instance.Saga.CorrelationId, x => x.Active, "Saga did not transition to ready");
        
        await harness.Bus.Publish<BookReserved>(new
        {
            ReservationId = reservationId,
            MemberId = memberId,
            bookId,
            Duration = TimeSpan.FromDays(14),
            Timestamp = InVar.Timestamp
        }, TestContext.Current.CancellationToken);
        
        await sagaHarness.AssertConsumed<BookReserved, ThankYouStateMachine, ThankYou>(
            filter => filter.Context.Message.BookId == bookId, "Message not consumed by saga");
        
        await sagaHarness.AssertState(instance.Saga.CorrelationId, x => x.Ready, "Saga did not transition to ready");
    }

    [Fact]
    public async Task Handle_Status_Checks()
    {
        await using var provider = CreateProvider();

        var harness = provider.GetTestHarness();

        await harness.Start();

        var bookId = Guid.NewGuid();
        var reservationId = Guid.NewGuid();
        var memberId = Guid.NewGuid();
        
        var sagaHarness = harness.GetSagaStateMachineHarness<ThankYouStateMachine, ThankYou>();
        var requestClient = harness.GetRequestClient<GetThankYouStatus>();

        
        await harness.Bus.Publish<BookCheckedOut>(new
        {
            CheckoutId = InVar.CorrelationId,
            MemberId = memberId,
            bookId,
            InVar.Timestamp
        }, TestContext.Current.CancellationToken);

        await sagaHarness
            .AssertConsumed<BookCheckedOut, ThankYouStateMachine, ThankYou>(
                filter => filter.Context.Message.BookId == bookId, "Message not consumed by saga");

        var instance = await sagaHarness.FirstCreated(x => x.BookId == bookId && x.MemberId == memberId);

        instance.Should().NotBeNull();
        
        var response = await requestClient.GetResponse<ThankYouStatus>(new { MemberId = memberId }, TestContext.Current.CancellationToken);
        
        response.Message.Status.Should().Be("Active");
        response.Message.MemberId.Should().Be(memberId);
        response.Message.BookId.Should().Be(bookId);
        
        await harness.Bus.Publish<BookReserved>(new
        {
            ReservationId = reservationId,
            MemberId = memberId,
            bookId,
            Duration = TimeSpan.FromDays(14),
            Timestamp = InVar.Timestamp
        }, TestContext.Current.CancellationToken);

        await sagaHarness.AssertConsumed<BookReserved, ThankYouStateMachine, ThankYou>(
            filter => filter.Context.Message.BookId == bookId, "Message not consumed by saga");

        response = await requestClient.GetResponse<ThankYouStatus>(new { MemberId = memberId }, TestContext.Current.CancellationToken);

        response.Message.Status.Should().Be("Ready");
        response.Message.MemberId.Should().Be(memberId);
        response.Message.BookId.Should().Be(bookId);
    }

    [Fact]
    public async Task Should_Get_Not_Found_Where_No_Saga_Exists()
    {
        await using var provider = CreateProvider();

        var harness = provider.GetTestHarness();

        await harness.Start();

        var requestClient = harness.GetRequestClient<GetThankYouStatus>();
        
        var notFound = InVar.CorrelationId;
        
        var response =
            await requestClient.GetResponse<ThankYouStatus>(new { MemberId = notFound }, TestContext.Current.CancellationToken);
        
        response.Message.MemberId.Should().Be(notFound);
        response.Message.Status.Should().Be("Not Found");
        
        response.Should().NotBeNull();
    }
    

    private static ServiceProvider CreateProvider() =>
        new ServiceCollection()
            .ConfigureMassTransit(x =>
            {
                x.AddSagaStateMachine<ThankYouStateMachine, ThankYou>();
                x.AddRequestClient<GetThankYouStatus>();
            })
            .BuildServiceProvider(true);
}
