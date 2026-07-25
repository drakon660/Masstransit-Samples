using AwesomeAssertions;
using ForkJointEnterprise.Api;
using ForkJointEnterprise.Api.Components.StateMachines;
using ForkJointEnterprise.Contracts;
using MassTransit;
using MassTransit.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace ForkJointEnterpriseTests;

public class OrderStateMachineTests
{
    public OrderStateMachineTests(ITestOutputHelper testOutputHelper)
    {
        TestOutputRelay.Use(testOutputHelper);
    }

    [Fact]
    public async Task Order_Happy_Path()
    {
        await using var provider = CreateProvider();

        var harness = provider.GetTestHarness();
        await harness.Start();

        var sagaHarness = harness.GetSagaStateMachineHarness<OrderStateMachine, OrderState>();

        var orderId = NewId.NextGuid();
        var burger = new Burger
        {
            BurgerId = NewId.NextGuid(),
            Weight = 0.5m,
            Cheese = true,
            Pickle = true,
            Onion = true,
            Ketchup = true,
            Mustard = true
        };
        var fry = new Fry { FryId = NewId.NextGuid(), Size = Size.Medium };
        var shake = new Shake { ShakeId = NewId.NextGuid(), Flavor = "Chocolate", Size = Size.Large };

        var requestClient = harness.GetRequestClient<SubmitOrder>();

        var response = await requestClient.GetResponse<OrderCompleted, OrderFaulted>(new
        {
            OrderId = orderId,
            Burgers = new[] { burger },
            Fries = new[] { fry },
            Shakes = new[] { shake },
            FryShakes = Array.Empty<FryShake>()
        }, TestContext.Current.CancellationToken);

        response.Is<OrderCompleted>(out var completed).Should().BeTrue("Order should complete successfully");

        completed!.Message.OrderId.Should().Be(orderId);
        completed.Message.Created.Should().NotBeNull();
        completed.Message.Completed.Should().NotBeNull();
        completed.Message.LinesCompleted.Should().HaveCount(3);
        completed.Message.LinesCompleted.Should().ContainKey(burger.BurgerId);
        completed.Message.LinesCompleted.Should().ContainKey(fry.FryId);
        completed.Message.LinesCompleted.Should().ContainKey(shake.ShakeId);

        await sagaHarness.AssertState(orderId, x => x.Completed, "Order saga did not reach Completed");

        await harness.Stop(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Order_No_Lettuce()
    {
        await using var provider = CreateProvider();

        var harness = provider.GetTestHarness();
        await harness.Start();

        var sagaHarness = harness.GetSagaStateMachineHarness<OrderStateMachine, OrderState>();

        var orderId = NewId.NextGuid();
        var burger = new Burger
        {
            BurgerId = NewId.NextGuid(),
            Weight = 0.5m,
            Cheese = true,
            Lettuce = true,
            Pickle = true,
            Ketchup = true,
            Mustard = true
        };

        var requestClient = harness.GetRequestClient<SubmitOrder>();

        var response = await requestClient.GetResponse<OrderCompleted, OrderFaulted>(new
        {
            OrderId = orderId,
            Burgers = new[] { burger },
            Fries = Array.Empty<Fry>(),
            Shakes = Array.Empty<Shake>(),
            FryShakes = Array.Empty<FryShake>()
        }, TestContext.Current.CancellationToken);

        response.Is<OrderFaulted>(out var faulted).Should().BeTrue("Order should fault when lettuce requested");

        faulted!.Message.OrderId.Should().Be(orderId);
        faulted.Message.Created.Should().NotBeNull();
        faulted.Message.Faulted.Should().NotBeNull();
        faulted.Message.LinesFaulted.Should().HaveCount(1);
        faulted.Message.LinesFaulted.Should().ContainKey(burger.BurgerId);
        faulted.Message.LinesCompleted.Should().BeEmpty();

        await sagaHarness.AssertState(orderId, x => x.Faulted, "Order saga did not reach Faulted");

        await harness.Stop(TestContext.Current.CancellationToken);
    }

    private static ServiceProvider CreateProvider() =>
        new ServiceCollection()
            .AddForkJointEnterpriseServices()
            .ConfigureMassTransit(x =>
            {
                x.AddForkJointEnterpriseComponents();
            })
            .BuildServiceProvider(true);
}
