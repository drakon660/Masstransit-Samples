namespace ForkJointEnterprise.Api.Components.StateMachines;

public class OrderFriesActivity(ILogger<OrderFriesActivity> logger) :
    IStateMachineActivity<OrderState, SubmitOrder>
{
    public void Probe(ProbeContext context) => context.CreateScope("orderFries");

    public void Accept(StateMachineVisitor visitor) => visitor.Visit(this);

    public async Task Execute(BehaviorContext<OrderState, SubmitOrder> context, IBehavior<OrderState, SubmitOrder> next)
    {
        if (context.Message.Fries != null)
        {
            logger.LogInformation("OrderFries fan-out: OrderId={OrderId} Count={Count}",
                context.Message.OrderId, context.Message.Fries.Length);

            await Task.WhenAll(context.Message.Fries.Select(fry =>
            {
                logger.LogInformation("OrderFries publishing OrderFry: OrderId={OrderId} FryId={FryId} Size={Size}",
                    context.Message.OrderId, fry.FryId, fry.Size);

                return context.Publish<OrderFry>(new
                {
                    context.Message.OrderId,
                    OrderLineId = fry.FryId,
                    fry.Size,
                    __RequestId = InVar.Id,
                    __ResponseAddress = context.ReceiveContext.InputAddress
                }, context.CancellationToken);
            }));
        }

        await next.Execute(context);
    }

    public Task Faulted<TException>(BehaviorExceptionContext<OrderState, SubmitOrder, TException> context, IBehavior<OrderState, SubmitOrder> next)
        where TException : Exception
    {
        logger.LogError(context.Exception, "OrderFries faulted: OrderId={OrderId}", context.Message.OrderId);
        return next.Faulted(context);
    }
}
