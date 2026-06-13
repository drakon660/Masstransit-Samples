namespace ForkJointEnterprise.Api.Components.StateMachines;

public class OrderFryShakesActivity(ILogger<OrderFryShakesActivity> logger) :
    IStateMachineActivity<OrderState, SubmitOrder>
{
    public void Probe(ProbeContext context) => context.CreateScope("orderFryShakes");

    public void Accept(StateMachineVisitor visitor) => visitor.Visit(this);

    public async Task Execute(BehaviorContext<OrderState, SubmitOrder> context, IBehavior<OrderState, SubmitOrder> next)
    {
        if (context.Message.FryShakes != null)
        {
            logger.LogInformation("Fanning out {Count} FryShakes for OrderId: {OrderId}",
                context.Message.FryShakes.Length, context.Message.OrderId);

            await Task.WhenAll(context.Message.FryShakes.Select(shake =>
            {
                logger.LogInformation("Publishing OrderFryShake for OrderId: {OrderId}, FryShakeId: {FryShakeId}, Flavor: {Flavor}, Size: {Size}",
                    context.Message.OrderId, shake.FryShakeId, shake.Flavor, shake.Size);

                return context.Publish<OrderFryShake>(new
                {
                    context.Message.OrderId,
                    OrderLineId = shake.FryShakeId,
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
        logger.LogError(context.Exception, "OrderFryShakesActivity faulted: OrderId={OrderId}",
            context.Message.OrderId);

        return next.Faulted(context);
    }
}
