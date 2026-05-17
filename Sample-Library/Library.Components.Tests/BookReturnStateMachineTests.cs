using AwesomeAssertions;
using Library.Components.Consumers;
using Library.Components.StateMachines.BookReturnStateMachine;
using Library.Components.Tests.Xunit;
using Library.Contracts;
using MassTransit;
using MassTransit.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Library.Components.Tests;

public class BookReturnStateMachineTests
{
    public BookReturnStateMachineTests(ITestOutputHelper testOutputHelper)
    {
        TestOutputRelay.Use(testOutputHelper);
    }

    [Fact]
    public async Task Should_Waive_The_Fine_For_A_Member_Under_18()
    {
        await using var provider = CreateProvider();

        var harness = provider.GetTestHarness();
        await harness.Start();   
        
        var sagaHarness = harness.GetSagaStateMachineHarness<BookReturnStateMachine, BookReturn>();
        
        var checkOutId = NewId.NextGuid();
        var bookId = NewId.NextGuid();
        var memberId = NewId.NextGuid();
        var now = DateTime.UtcNow;

        await harness.Bus.Publish<BookReturned>(new
        {
            CheckOutId = checkOutId,
            InVar.Timestamp,
            BookId = bookId,
            MemberId = memberId,
            MemberAge = 17,
            CheckOutDate = now - TimeSpan.FromDays(28),
            DueDate = now - TimeSpan.FromDays(14),
            ReturnDate = now,
        }, TestContext.Current.CancellationToken);
        
        await sagaHarness.AssertCreated(checkOutId);
        
        await harness.AssertConsumed<ChargeMemberFine>("Fine not consumed");
        await harness.AssertConsumed<FineWaived>("Fine was not waived");
        
        // ChargeFine has a request timeout, so MassTransit schedules a timeout through Quartz.
        // When a fine response arrives, MassTransit sends Quartz a CancelScheduledMessage for that timeout.
        // Wait for the bus to go idle so Quartz consumes the cancellation before the provider is disposed.
        await harness.InactivityTask;
    }

    [Fact]
    public async Task Should_Handle_The_Request_Fault()
    {
        await using var provider = CreateProvider(configure: x =>
        {
            x.AddConsumer<BadChargeFineConsumer>().Endpoint(e =>
                e.Name = KebabCaseEndpointNameFormatter.Instance.Consumer<ChargeFineConsumer>());
            
        }, addChargeFineConsumer: false);

        var harness = provider.GetTestHarness();
        await harness.Start();   
        
        var sagaHarness = harness.GetSagaStateMachineHarness<BookReturnStateMachine, BookReturn>();
        var checkOutId = NewId.NextGuid();
        var bookId = NewId.NextGuid();
        var memberId = NewId.NextGuid();

        var now = DateTime.UtcNow;

        await harness.Bus.Publish<BookReturned>(new
        {
            CheckOutId = checkOutId,
            InVar.Timestamp,
            BookId = bookId,
            MemberId = memberId,
            MemberAge = 17,
            CheckOutDate = now - TimeSpan.FromDays(28),
            DueDate = now - TimeSpan.FromDays(14),
            ReturnDate = now,
        },TestContext.Current.CancellationToken);
        
        await sagaHarness.AssertCreated(checkOutId);
        await harness.AssertConsumed<ChargeMemberFine>("Fine not consumed");
        await sagaHarness.AssertConsumed<Fault<ChargeMemberFine>, BookReturnStateMachine, BookReturn>(
            "Fault not consumed by saga");
        await sagaHarness.AssertState(checkOutId, x => x.FailedToFineMember,
            "Saga did not transition to the failed state");
        await harness.InactivityTask;
    }

    [Fact]
    public async Task Should_Charge_The_Fine_When_The_Client_Does_Not_Accept_Fine_Waived()
    {
        await using var provider = CreateProvider();

        var harness = provider.GetTestHarness();
        await harness.Start();

        var requestClient = harness.GetRequestClient<ChargeMemberFine>();
        var memberId = NewId.NextGuid();

        var response = await requestClient.GetResponse<FineCharged>(new
        {
            MemberId = memberId,
            MemberAge = 17,
            Amount = 123.45m
        }, TestContext.Current.CancellationToken);

        response.Message.MemberId.Should().Be(memberId);
        response.Message.Amount.Should().Be(123.45m);
        await harness.AssertConsumed<ChargeMemberFine>("Fine not consumed");
        await harness.AssertConsumed<FineCharged>("Fine not charged");
    }

    private static ServiceProvider CreateProvider(
        Action<IBusRegistrationConfigurator> configure = null,
        bool addChargeFineConsumer = true) =>
        new ServiceCollection()
            .ConfigureMassTransit(x =>
            {
                if (addChargeFineConsumer)
                    x.AddConsumer<ChargeFineConsumer>();

                x.AddSagaStateMachine<BookReturnStateMachine, BookReturn>();
                
                configure?.Invoke(x);
            })
            .BuildServiceProvider(true);
    
    class BadChargeFineConsumer :
        IConsumer<ChargeMemberFine>
    {
        public async Task Consume(ConsumeContext<ChargeMemberFine> context)
        {
            await Task.Delay(1000);

            throw new InvalidOperationException("No money!");
        }
    }
}
