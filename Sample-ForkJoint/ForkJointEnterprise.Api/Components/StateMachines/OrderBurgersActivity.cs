namespace ForkJointEnterprise.Api.Components.StateMachines;

public class OrderBurgersActivity :
    IStateMachineActivity<OrderState, SubmitOrder>
{
    public void Probe(ProbeContext context) => context.CreateScope("orderBurgers");

    public void Accept(StateMachineVisitor visitor) => visitor.Visit(this);

    public async Task Execute(BehaviorContext<OrderState, SubmitOrder> context, IBehavior<OrderState, SubmitOrder> next)
    {
        if (context.Message.Burgers != null)
            await Task.WhenAll(context.Message.Burgers.Select(burger => context.Publish<OrderBurger>(new
            {
                context.Message.OrderId,
                Burger = burger,
                __RequestId = InVar.Id,
                __ResponseAddress = context.ReceiveContext.InputAddress
            }, context.CancellationToken)));

        await next.Execute(context);
    }

    public Task Faulted<TException>(BehaviorExceptionContext<OrderState, SubmitOrder, TException> context, IBehavior<OrderState, SubmitOrder> next)
        where TException : Exception
    {
        return next.Faulted(context);
    }
}
