using AwesomeAssertions;
using Library.Components.Services;
using Library.Components.StateMachines.CheckOut;
using Library.Contracts;
using MassTransit;
using MassTransit.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Library.Components.Tests;

public class CheckOutStateMachineTests
{
    [Fact]
    public async Task Should_Create_A_Saga_Instance()
    {
        await using var provider = CreateProvider();

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
        (saga.DueDate - saga.CheckoutDate).Should().Be(TimeSpan.FromDays(14));

        await harness.AssertPublished<NotifyMemberDueDate>("Due Date Event Not Published");
    }
    
    [Fact]
    public async Task When_CheckedOut_Is_Renewed_Should_Renew_Existing_CheckOut()
    {
        await using var provider = CreateProvider();

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
        (saga.DueDate - saga.CheckoutDate).Should().Be(TimeSpan.FromDays(14));

        await harness.AssertPublished<NotifyMemberDueDate>("Due Date Event Not Published");
         
        var requestClient = harness.GetRequestClient<RenewCheckOut>();
        var (renewed, notFound) = await requestClient.GetResponse<CheckOutRenewed, CheckOutNotFound>(new
        {
            CheckOutId = checkOutId,
        }, TestContext.Current.CancellationToken);

        renewed.IsCompletedSuccessfully.Should().BeTrue();
        notFound.IsCompletedSuccessfully.Should().BeFalse();

        var result = await renewed;
        (result.Message.DueDate - saga.CheckoutDate).Should().Be(TimeSpan.FromDays(14));
    }

    [Fact]
    public async Task When_CheckedOut_Is_Renewed_Should_Not_Complete_On_Missing_CheckOut()
    {
        await using var provider = CreateProvider();

        var harness = provider.GetTestHarness();
        await harness.Start();

        var checkOutId = NewId.NextGuid();
        
        var requestClient = harness.GetRequestClient<RenewCheckOut>();
        var (renewed, notFound) = await requestClient.GetResponse<CheckOutRenewed, CheckOutNotFound>(new
        {
            CheckOutId = checkOutId,
        }, TestContext.Current.CancellationToken);

        if (renewed.IsCompletedSuccessfully)
        {
            var result = await renewed;
            (result.Message.DueDate - DateTime.UtcNow).Should().Be(TimeSpan.FromDays(14));    
        }
        
        notFound.IsCompletedSuccessfully.Should().BeTrue();
        renewed.IsCompletedSuccessfully.Should().BeFalse();
    }

    private static ServiceProvider CreateProvider() =>
        new ServiceCollection()
            .AddSingleton<CheckOutSettings>(new TestCheckOutSettings())
            .AddScoped<IMemberRegistry, AnyMemberIsValidMemberRegistry>()
            .ConfigureMassTransit(x =>
            {
                x.AddSagaStateMachine<CheckOutStateMachine, CheckOut>();
            })
            .BuildServiceProvider(true);

    private sealed class TestCheckOutSettings : CheckOutSettings
    {
        public TimeSpan CheckOutDuration { get; set; } = TimeSpan.FromDays(14);
    }

    private sealed class AnyMemberIsValidMemberRegistry : IMemberRegistry
    {
        public Task<bool> IsMemberValid(Guid memberId) => Task.FromResult(true);
    }
}
