using AwesomeAssertions;
using Library.Components.Consumers;
using Library.Components.Services;
using Library.Components.StateMachines.CheckOut;
using Library.Components.Tests.Xunit;
using Library.Contracts;
using MassTransit;
using MassTransit.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Library.Components.Tests;

public class CheckOutStateMachineTests
{
    public CheckOutStateMachineTests(ITestOutputHelper testOutputHelper)
    {
        TestOutputRelay.Use(testOutputHelper);
    }

    [Fact]
    public async Task Should_Create_A_Saga_Instance()
    {
        await using var provider = CreateProvider(new TestCheckOutSettings(), addRenewCheckOutClient: true);
        using var cts = CreateTimeoutToken();

        var harness = provider.GetTestHarness();
        await harness.Start();

        var sagaHarness = harness.GetSagaStateMachineHarness<CheckOutStateMachine, CheckOut>();

        var checkOutId = NewId.NextGuid();
        var bookId = NewId.NextGuid();
        var memberId = NewId.NextGuid();

        await harness.PublishBookCheckedOut(bookId, memberId, checkOutId);

        await harness.AssertConsumed<BookCheckedOut>("Message not consumed");
        await sagaHarness.AssertConsumed<BookCheckedOut, CheckOutStateMachine, CheckOut>("Message not consumed by saga");
        await sagaHarness.AssertCreated(checkOutId);
        await sagaHarness.AssertState(checkOutId, x => x.CheckedOut, "Saga not in CheckedOut state");

        var saga = sagaHarness.Created.ContainsInState(checkOutId, sagaHarness.StateMachine, sagaHarness.StateMachine.CheckedOut);
        saga.Should().NotBeNull();
        saga.BookId.Should().Be(bookId);
        saga.MemberId.Should().Be(memberId);
        (saga.DueDate - saga.CheckOutDate).Should().Be(TimeSpan.FromDays(14));

        await harness.AssertPublished<NotifyMemberDueDate>("Due Date Event Not Published");
    }

    [Fact]
    public async Task When_CheckedOut_Is_Renewed_Should_Renew_Existing_CheckOut()
    {
        await using var provider = CreateProvider(new TestCheckOutSettings(), addRenewCheckOutClient: true);
        using var cts = CreateTimeoutToken();

        var harness = provider.GetTestHarness();
        await harness.Start();

        var sagaHarness = harness.GetSagaStateMachineHarness<CheckOutStateMachine, CheckOut>();

        var checkOutId = NewId.NextGuid();
        var bookId = NewId.NextGuid();
        var memberId = NewId.NextGuid();

        await harness.PublishBookCheckedOut(bookId, memberId, checkOutId);

        await harness.AssertConsumed<BookCheckedOut>("Message not consumed");
        await sagaHarness.AssertConsumed<BookCheckedOut, CheckOutStateMachine, CheckOut>("Message not consumed by saga");
        await sagaHarness.AssertCreated(checkOutId);
        await sagaHarness.AssertState(checkOutId, x => x.CheckedOut, "Saga not in CheckedOut state");

        var saga = sagaHarness.Created.ContainsInState(checkOutId, sagaHarness.StateMachine, sagaHarness.StateMachine.CheckedOut);
        saga.Should().NotBeNull();
        saga.BookId.Should().Be(bookId);
        saga.MemberId.Should().Be(memberId);
        (saga.DueDate - saga.CheckOutDate).Should().Be(TimeSpan.FromDays(14));

        await harness.AssertPublished<NotifyMemberDueDate>("Due Date Event Not Published");

        var requestClient = harness.GetRequestClient<RenewCheckOut>();

        Response<CheckOutRenewed, CheckOutNotFound> response =
            await requestClient.GetResponse<CheckOutRenewed, CheckOutNotFound>(new { checkOutId }, cts.Token);

        response.Is(out Response<CheckOutNotFound> _).Should().BeFalse();
        response.Is(out Response<CheckOutRenewed> renewed).Should().BeTrue();

        (renewed.Message.DueDate - saga.CheckOutDate).Should().BeGreaterThanOrEqualTo(TimeSpan.FromDays(14));
    }

    [Fact]
    public async Task When_CheckedOut_Is_Renewed_Should_Not_Complete_On_Missing_CheckOut()
    {
        await using var provider = CreateProvider(new TestCheckOutSettings());
        using var cts = CreateTimeoutToken();

        var harness = provider.GetTestHarness();
        await harness.Start();

        var checkOutId = NewId.NextGuid();

        var requestClient = harness.GetRequestClient<RenewCheckOut>();

        Response<CheckOutRenewed, CheckOutNotFound> response =
            await requestClient.GetResponse<CheckOutRenewed, CheckOutNotFound>(new { checkOutId }, cts.Token);

        response.Is(out Response<CheckOutRenewed> _).Should().BeFalse();
        response.Is(out Response<CheckOutNotFound> _).Should().BeTrue();
    }

    [Fact]
    public async Task When_CheckedOut_Is_Renewed_Should_Renew_An_Existing_CheckOut_Up_To_The_Limit()
    {
        await using var provider = CreateProvider(
            new TestCheckOutSettings { CheckOutDurationLimit = TimeSpan.FromDays(13) },
            addRenewCheckOutClient: true);
        using var cts = CreateTimeoutToken();

        var harness = provider.GetTestHarness();
        await harness.Start();

        var sagaHarness = harness.GetSagaStateMachineHarness<CheckOutStateMachine, CheckOut>();

        var checkOutId = NewId.NextGuid();
        var bookId = NewId.NextGuid();
        var memberId = NewId.NextGuid();

        var now = DateTime.UtcNow;
        var checkedOutAt = DateTime.UtcNow;
        
        await harness.PublishBookCheckedOut(bookId, memberId, checkOutId);

        await sagaHarness.AssertConsumed<BookCheckedOut, CheckOutStateMachine, CheckOut>("Message not consumed by saga");
        await sagaHarness.AssertCreated(checkOutId);
        await sagaHarness.AssertState(checkOutId, x => x.CheckedOut, "Saga not in CheckedOut state");

        var requestClient = harness.GetRequestClient<RenewCheckOut>();
        
        //why false, because we are not ready to send because I add new response types
        Response<CheckOutRenewed, CheckOutNotFound, CheckOutDurationLimitReached> response =
            await requestClient.GetResponse<CheckOutRenewed, CheckOutNotFound, CheckOutDurationLimitReached>(new { checkOutId }, cts.Token);

        now = DateTime.UtcNow;

        response.Is(out Response<CheckOutNotFound> _).Should().BeFalse();
        response.Is(out Response<CheckOutRenewed> _).Should().BeFalse();
        response.Is(out Response<CheckOutDurationLimitReached> limitReached).Should().BeTrue();
        
        limitReached.Message.DueDate.Should().BeBefore(now + TimeSpan.FromDays(14));
        limitReached.Message.DueDate.Should().BeOnOrAfter(checkedOutAt + TimeSpan.FromDays(13));
    }

    [Fact]
    public async Task When_CheckedOut_It_Should_Add_The_Book_To_Member_Collection()
    {
        await using var provider = CreateProvider(new TestCheckOutSettings { CheckOutDurationLimit = TimeSpan.FromDays(13) }, register:(
            x =>
            {
                x.AddConsumer<MemberCollectionConsumer>();
            }));
        using var cts = CreateTimeoutToken();

        var harness = provider.GetTestHarness();
        await harness.Start();

        var sagaHarness = harness.GetSagaStateMachineHarness<CheckOutStateMachine, CheckOut>();

        var checkOutId = NewId.NextGuid();
        var bookId = NewId.NextGuid();
        var memberId = NewId.NextGuid();
        
        await harness.PublishBookCheckedOut(bookId, memberId, checkOutId);
        
        await sagaHarness.AssertCreated(checkOutId);
        await sagaHarness.AssertState(checkOutId, x => x.CheckedOut, "Saga not in CheckedOut state");

        await harness.AssertPublished<AddBookToMemberCollection>("Add to Collection not published");
        await harness.AssertConsumed<AddBookToMemberCollection>("Add to Collection not consumed");
    }
    
    
    [Fact]
    public async Task When_CheckedOut_It_Should_Handle_the_fault_message_via_correlation()
    {
        await using var provider = CreateProvider(new TestCheckOutSettings { CheckOutDurationLimit = TimeSpan.FromDays(13) }, register:(
            x =>
            {
                x.AddConsumer<BadMemberCollectionConsumer>();
            }));
        using var cts = CreateTimeoutToken();

        var harness = provider.GetTestHarness();
        await harness.Start();

        var sagaHarness = harness.GetSagaStateMachineHarness<CheckOutStateMachine, CheckOut>();

        var checkOutId = NewId.NextGuid();
        var bookId = NewId.NextGuid();
        var memberId = NewId.NextGuid();
        
        await harness.PublishBookCheckedOut(bookId, memberId, checkOutId);
        
        await sagaHarness.AssertCreated(checkOutId);
        await sagaHarness.AssertState(checkOutId, x => x.CheckedOut, "Saga not in CheckedOut state");

        await harness.AssertPublished<AddBookToMemberCollection>("Add to Collection not published");
        await harness.AssertConsumed<AddBookToMemberCollection>("Add to Collection not consumed");
        
        await harness.AssertPublished<Fault<AddBookToMemberCollection>>("fault not published");
        await sagaHarness.AssertConsumed<Fault<AddBookToMemberCollection>, CheckOutStateMachine, CheckOut>(
            "fault not consumed by saga");
    }
    
    private static CancellationTokenSource CreateTimeoutToken(TimeSpan? timeout = null)
    {
        var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        cts.CancelAfter(timeout ?? TimeSpan.FromSeconds(3));
        return cts;
    }

    private static ServiceProvider CreateProvider(CheckOutSettings checkOutSettings, bool addRenewCheckOutClient = false, 
        Action<IBusRegistrationConfigurator> register = null) =>
        new ServiceCollection()
            .AddSingleton(checkOutSettings)
            .AddScoped<IMemberRegistry, AnyMemberIsValidMemberRegistry>()
            .ConfigureMassTransit(x =>
            {
                x.AddSagaStateMachine<CheckOutStateMachine, CheckOut>();
                
                if (addRenewCheckOutClient)
                    x.AddRequestClient<RenewCheckOut>();

                register?.Invoke(x);
            })
            .BuildServiceProvider(true);

    private sealed class TestCheckOutSettings : CheckOutSettings
    {
        public TimeSpan CheckOutDuration { get; set; } = TimeSpan.FromDays(14);
        public TimeSpan CheckOutDurationLimit { get; set; } = TimeSpan.FromDays(30);
    }

    private sealed class AnyMemberIsValidMemberRegistry : IMemberRegistry
    {
        public Task<bool> IsMemberValid(Guid memberId) => Task.FromResult(true);
    }
    
    class BadMemberCollectionConsumer : IConsumer<AddBookToMemberCollection>
    {
        public async Task Consume(ConsumeContext<AddBookToMemberCollection> context)
        {
            await Task.Delay(1000);
            throw new InvalidOperationException("The member is overdrawn");
        }
    }
}
