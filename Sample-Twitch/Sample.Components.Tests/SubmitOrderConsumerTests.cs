using AwesomeAssertions;
using MassTransit;
using MassTransit.Testing;
using Microsoft.Extensions.DependencyInjection;
using Sample.Components.Consumers;
using Sample.Components.Tests.Xunit;
using Sample.Contracts;

namespace Sample.Components.Tests;

public class SubmitOrderConsumerTests
{
    public SubmitOrderConsumerTests(ITestOutputHelper output)
    {
        TestOutputRelay.Use(output);
    }

    [Fact]
    public async Task When_An_Order_Request_Is_Consumed_Should_Respond_With_Acceptance_If_Ok()
    {
        await using var context = await StartTestHarness();

        var requestClient = context.Harness.GetRequestClient<SubmitOrder>();

        var orderId = NewId.NextGuid();
        const string customerNumber = "12345";
        const string paymentCardNumber = "5999-1234-5678-9012";

        var response = await requestClient.GetResponse<OrderSubmissionAccepted>(new
        {
            OrderId = orderId,
            InVar.Timestamp,
            CustomerNumber = customerNumber,
            PaymentCardNumber = paymentCardNumber
        }, TestContext.Current.CancellationToken);

        response.Message.OrderId.Should().Be(orderId);
        response.Message.CustomerNumber.Should().Be(customerNumber);
        response.Message.Timestamp.Should().NotBe(default);

        await context.Harness.AssertConsumed<SubmitOrder>("SubmitOrder not consumed");
        await context.Harness.AssertSent<OrderSubmissionAccepted>("OrderSubmissionAccepted not sent");
        await context.Harness.AssertPublished<OrderSubmitted>("OrderSubmitted not published");
    }

    [Fact]
    public async Task When_An_Order_Request_Is_Consumed_Should_Respond_With_Rejected_If_Customer_Number_Starts_With()
    {
        await using var context = await StartTestHarness();
        
        var orderId = NewId.NextGuid();
        const string customerNumber = "TEST123";
        const string paymentCardNumber = "5999-1234-5678-9012";
        
        var requestClient = context.Harness.GetRequestClient<SubmitOrder>();
        
        var response = await requestClient.GetResponse<OrderSubmissionRejected>(new
        {
            OrderId = orderId,
            InVar.Timestamp,
            CustomerNumber = customerNumber,
            PaymentCardNumber = paymentCardNumber
        }, TestContext.Current.CancellationToken);

        response.Message.OrderId.Should().Be(orderId);
        response.Message.CustomerNumber.Should().Be(customerNumber);
        response.Message.Timestamp.Should().NotBe(default);
        response.Message.Reason.Should().Be($"Test Customer cannot submit orders: {customerNumber}");

        await context.Harness.AssertConsumed<SubmitOrder>("SubmitOrder not consumed");
        await context.Harness.AssertSent<OrderSubmissionRejected>("OrderSubmissionRejected not sent");
        await context.Harness.AssertNotPublished<OrderSubmitted>("rejected orders should not publish OrderSubmitted");
    }

    [Fact]
    public async Task When_An_Order_Request_Is_Consumed_Should_Consume_Submit_Order_Commands()
    {
        await using var context = await StartTestHarness();
        
        var orderId = NewId.NextGuid();
        const string customerNumber = "12345";
        const string paymentCardNumber = "5999-1234-5678-9012";

        var endpoint = await context.Harness.GetConsumerEndpoint<SubmitOrderConsumer>();
        await endpoint.Send<SubmitOrder>(new
        {
            OrderId = orderId,
            InVar.Timestamp,
            CustomerNumber = customerNumber,
            PaymentCardNumber = paymentCardNumber
        }, TestContext.Current.CancellationToken);

        await context.Harness.AssertConsumed<SubmitOrder>("SubmitOrder not consumed");
        await context.Harness.AssertNotSent<OrderSubmissionAccepted>("OrderSubmissionAccepted should not be sent for commands");
        await context.Harness.AssertNotSent<OrderSubmissionRejected>("OrderSubmissionRejected should not be sent for commands");
    }

    [Fact]
    public async Task When_An_Order_Request_Is_Consumed_Should_Not_Publish_Order_Submitted_Event_When_Rejected()
    {
        await using var context = await StartTestHarness();
        
        var orderId = NewId.NextGuid();
        const string customerNumber = "TEST123";
        const string paymentCardNumber = "5999-1234-5678-9012";

        var endpoint = await context.Harness.GetConsumerEndpoint<SubmitOrderConsumer>();
        await endpoint.Send<SubmitOrder>(new
        {
            OrderId = orderId,
            InVar.Timestamp,
            CustomerNumber = customerNumber,
            PaymentCardNumber = paymentCardNumber
        }, TestContext.Current.CancellationToken);

        await context.Harness.AssertConsumed<SubmitOrder>("SubmitOrder not consumed");
        await context.Harness.AssertNotPublished<OrderSubmitted>("rejected orders should not publish OrderSubmitted");
    }
    
    [Fact]
    public async Task When_An_Order_Request_Is_Consumed_Should_Publish_Order_Submitted_Event()
    {
        await using var context = await StartTestHarness();

        var orderId = NewId.NextGuid();
        const string customerNumber = "12345";
        const string paymentCardNumber = "5999-1234-5678-9012";

        var endpoint = await context.Harness.GetConsumerEndpoint<SubmitOrderConsumer>();
        await endpoint.Send<SubmitOrder>(new
        {
            OrderId = orderId,
            InVar.Timestamp,
            CustomerNumber = customerNumber,
            PaymentCardNumber = paymentCardNumber
        }, TestContext.Current.CancellationToken);

        await context.Harness.AssertConsumed<SubmitOrder>("SubmitOrder not consumed");
        await context.Harness.AssertPublished<OrderSubmitted>("OrderSubmitted not published");
    }

    private static ServiceProvider CreateProvider() =>
        new ServiceCollection()
            .ConfigureMassTransit(x =>
            {
                x.AddConsumer<SubmitOrderConsumer>();
                x.AddRequestClient<CheckOrder>();
            })
            .BuildServiceProvider(true);

    private static async Task<TestHarnessContext> StartTestHarness()
    {
        var provider = CreateProvider();
        var harness = provider.GetTestHarness();

        await harness.Start();

        return new TestHarnessContext(
            provider,
            harness);
    }
    
    private sealed class TestHarnessContext(
        ServiceProvider provider,
        ITestHarness harness) : IAsyncDisposable
    {
        public ITestHarness Harness { get; } = harness;
        
        public ValueTask DisposeAsync() => provider.DisposeAsync();
    }
}
