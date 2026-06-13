namespace ForkJointEnterprise.Api.Components.StateMachines;

public class OrderFryShakeActivity(ILogger<OrderFryShakeActivity> logger) :
    IStateMachineActivity<FryShakeState, OrderFryShake>
{
    public void Probe(ProbeContext context) => context.CreateScope("orderFryShake");

    public void Accept(StateMachineVisitor visitor) => visitor.Visit(this);

    public async Task Execute(BehaviorContext<FryShakeState, OrderFryShake> context, IBehavior<FryShakeState, OrderFryShake> next)
    {
        logger.LogInformation("Publishing OrderFry for OrderId: {OrderId}, LineId: {OrderLineId}, Size: {Size}",
            context.Message.OrderId, context.Message.OrderLineId, context.Message.Size);

        await context.Publish<OrderFry>(new
        {
            context.Message.OrderId,
            context.Message.OrderLineId,
            context.Message.Size,
            __RequestId = InVar.Id,
            __ResponseAddress = context.ReceiveContext.InputAddress
        }, context.CancellationToken);

        logger.LogInformation("Publishing OrderShake for OrderId: {OrderId}, LineId: {OrderLineId}, Flavor: {Flavor}, Size: {Size}",
            context.Message.OrderId, context.Message.OrderLineId, context.Message.Flavor, context.Message.Size);

        await context.Publish<OrderShake>(new
        {
            context.Message.OrderId,
            context.Message.OrderLineId,
            context.Message.Flavor,
            context.Message.Size,
            __RequestId = InVar.Id,
            __ResponseAddress = context.ReceiveContext.InputAddress
        }, context.CancellationToken);

        await next.Execute(context);
    }

    public Task Faulted<TException>(BehaviorExceptionContext<FryShakeState, OrderFryShake, TException> context, IBehavior<FryShakeState, OrderFryShake> next)
        where TException : Exception
    {
        logger.LogError(context.Exception, "OrderFryShakeActivity faulted: OrderId={OrderId} LineId={LineId}",
            context.Saga.OrderId, context.Saga.CorrelationId);

        return next.Faulted(context);
    }
}
