using AwesomeAssertions;
using MassTransit;
using MassTransit.SagaStateMachine;
using MassTransit.Testing;
using MassTransit.Visualizer;
using Microsoft.Extensions.DependencyInjection;
using Sample.Components.StateMachines;
using Sample.Components.Tests.Xunit;
using Sample.Contracts;

namespace Sample.Components.Tests;

public class OrderStateMachineTests
{
    private readonly ITestOutputHelper _output;

    public OrderStateMachineTests(ITestOutputHelper output)
    {
        _output = output;
        TestOutputRelay.Use(output);
    }

    [Fact]
    public async Task Should_Create_A_State_Instance()
    {
        await using var context = await StartTestHarness();

        var orderId = NewId.NextGuid();
        const string customerNumber = "12345";
        const string paymentCardNumber = "5999-1234-5678-9012";

        await context.Harness.Bus.Publish<OrderSubmitted>(new
        {
            OrderId = orderId,
            InVar.Timestamp,
            CustomerNumber = customerNumber,
            PaymentCardNumber = paymentCardNumber
        }, TestContext.Current.CancellationToken);

        await context.SagaHarness.AssertState(
            x => x.CorrelationId == orderId &&
                 x.CustomerNumber == customerNumber &&
                 x.PaymentCardNumber == paymentCardNumber,
            x => x.Submitted,
            "the OrderSubmitted event should create a Submitted saga with the submitted order data");
    }

    [Fact]
    public async Task Should_Respond_To_Status_Checks()
    {
        await using var context = await StartTestHarness();

        var orderId = NewId.NextGuid();
        const string customerNumber = "12345";
        const string paymentCardNumber = "5999-1234-5678-9012";

        await context.Harness.Bus.Publish<OrderSubmitted>(new
        {
            OrderId = orderId,
            InVar.Timestamp,
            CustomerNumber = customerNumber,
            PaymentCardNumber = paymentCardNumber
        }, TestContext.Current.CancellationToken);

        await context.SagaHarness.AssertState(
            x => x.CorrelationId == orderId &&
                 x.CustomerNumber == customerNumber &&
                 x.PaymentCardNumber == paymentCardNumber,
            x => x.Submitted,
            "the order must exist before it can respond to status checks");

        var requestClient = context.Harness.GetRequestClient<CheckOrder>();
        
        var response = await requestClient.GetResponse<OrderStatus>(new { OrderId = orderId }, TestContext.Current.CancellationToken);

        response.Message.OrderId.Should().Be(orderId);
        response.Message.State.Should().Be(nameof(OrderStateMachine.Submitted));
        response.Message.PaymentCardNumber.Should().Be(paymentCardNumber);
    }

    [Fact]
    public async Task Should_Cancel_When_Customer_Account_Closed()
    {
        await using var context = await StartTestHarness();

        var orderId = NewId.NextGuid();
        const string customerNumber = "12345";
        const string paymentCardNumber = "5999-1234-5678-9012";

        await context.Harness.Bus.Publish<OrderSubmitted>(new
        {
            OrderId = orderId,
            InVar.Timestamp,
            CustomerNumber = customerNumber,
            PaymentCardNumber = paymentCardNumber
        }, TestContext.Current.CancellationToken);

        await context.SagaHarness.AssertState(
            x => x.CorrelationId == orderId &&
                 x.CustomerNumber == customerNumber &&
                 x.PaymentCardNumber == paymentCardNumber,
            x => x.Submitted,
            "the order must exist before the customer account can be closed");

        await context.Harness.Bus.Publish<CustomerAccountClosed>(new
        {
            CustomerId = InVar.Id,
            CustomerNumber = customerNumber
        }, TestContext.Current.CancellationToken);

        await context.SagaHarness.AssertState(
            x => x.CorrelationId == orderId &&
                 x.CustomerNumber == customerNumber &&
                 x.PaymentCardNumber == paymentCardNumber,
            x => x.Canceled,
            "saga instance not canceled");
    }

    [Fact]
    public async Task Should_Accept_When_Order_Is_Submitted()
    {
        await using var context = await StartTestHarness();

        var orderId = NewId.NextGuid();
        const string customerNumber = "12345";
        const string paymentCardNumber = "5999-1234-5678-9012";

        await context.Harness.Bus.Publish<OrderSubmitted>(new
        {
            OrderId = orderId,
            InVar.Timestamp,
            CustomerNumber = customerNumber,
            PaymentCardNumber = paymentCardNumber
        }, TestContext.Current.CancellationToken);
        
        await context.SagaHarness.AssertState(
            x => x.CorrelationId == orderId &&
                 x.CustomerNumber == customerNumber &&
                 x.PaymentCardNumber == paymentCardNumber,
            x => x.Submitted,
            "the order must exist before it can be accepted");
        
        await context.Harness.Bus.Publish<OrderAccepted>(new
        {
            OrderId = orderId,
            InVar.Timestamp
        }, TestContext.Current.CancellationToken);

        await context.SagaHarness.AssertState(
            x => x.CorrelationId == orderId &&
                 x.CustomerNumber == customerNumber &&
                 x.PaymentCardNumber == paymentCardNumber,
            x => x.Accepted,
            "the OrderAccepted event should transition the saga to Accepted");
    }
    
    [Fact]
    public void Show_Me_The_State_Machine()
    {
        var orderStateMachine = new OrderStateMachine();

        var graph = orderStateMachine.GetGraph();

        var generator = new StateMachineGraphvizGenerator(graph);

        string dots = generator.CreateDotFile();

        dots.Should().Contain("digraph");
        _output.WriteLine(dots);
    }

    private static ServiceProvider CreateProvider() =>
        new ServiceCollection()
            .ConfigureMassTransit(x =>
            {
                x.AddRequestClient<CheckOrder>();
                x.AddSagaStateMachine<OrderStateMachine, OrderState>();
            })
            .BuildServiceProvider(true);

    private static async Task<TestHarnessContext> StartTestHarness()
    {
        var provider = CreateProvider();
        var harness = provider.GetTestHarness();

        await harness.Start();

        return new TestHarnessContext(
            provider,
            harness,
            harness.GetSagaStateMachineHarness<OrderStateMachine, OrderState>());
    }

    private sealed class TestHarnessContext(
        ServiceProvider provider,
        ITestHarness harness,
        ISagaStateMachineTestHarness<OrderStateMachine, OrderState> sagaHarness) : IAsyncDisposable
    {
        public ITestHarness Harness { get; } = harness;
        public ISagaStateMachineTestHarness<OrderStateMachine, OrderState> SagaHarness { get; } = sagaHarness;

        public ValueTask DisposeAsync() => provider.DisposeAsync();
    }
}
