namespace ForkJointEnterprise.Api.Components.StateMachines;

public class OrderShakesActivity(ILogger<OrderShakesActivity> logger) :
    IStateMachineActivity<OrderState, SubmitOrder>
{
    public void Probe(ProbeContext context) => context.CreateScope("orderShakes");

    public void Accept(StateMachineVisitor visitor) => visitor.Visit(this);

    public async Task Execute(BehaviorContext<OrderState, SubmitOrder> context, IBehavior<OrderState, SubmitOrder> next)
    {
        if (context.Message.Shakes != null)
        {
            logger.LogInformation("OrderShakes fan-out: OrderId={OrderId} Count={Count}",
                context.Message.OrderId, context.Message.Shakes.Length);

            await Task.WhenAll(context.Message.Shakes.Select(shake =>
            {
                logger.LogInformation("OrderShakes publishing OrderShake: OrderId={OrderId} ShakeId={ShakeId} Flavor={Flavor} Size={Size}",
                    context.Message.OrderId, shake.ShakeId, shake.Flavor, shake.Size);

                return context.Publish<OrderShake>(new
                {
                    context.Message.OrderId,
                    OrderLineId = shake.ShakeId,
                    shake.Flavor,
                    shake.Size,
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
        logger.LogError(context.Exception, "OrderShakes faulted: OrderId={OrderId}", context.Message.OrderId);
        return next.Faulted(context);
    }
}
