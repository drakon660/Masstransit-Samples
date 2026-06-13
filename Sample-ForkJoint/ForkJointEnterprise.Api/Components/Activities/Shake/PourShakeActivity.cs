namespace ForkJointEnterprise.Api.Components.StateMachines;

public class PourShakeActivity(ILogger<PourShakeActivity> logger) :
    IStateMachineActivity<ShakeState>
{
    public void Probe(ProbeContext context) => context.CreateScope("pourShake");

    public void Accept(StateMachineVisitor visitor) => visitor.Visit(this);

    public async Task Execute(BehaviorContext<ShakeState> context, IBehavior<ShakeState> next)
    {
        await ExecuteCore(context);
        await next.Execute(context);
    }

    public async Task Execute<T>(BehaviorContext<ShakeState, T> context, IBehavior<ShakeState, T> next) where T : class
    {
        await ExecuteCore(context);
        await next.Execute(context);
    }

    async Task ExecuteCore(BehaviorContext<ShakeState> context)
    {
        logger.LogInformation("PourShake publishing: OrderId={OrderId} LineId={LineId} Flavor={Flavor} Size={Size}",
            context.Saga.OrderId, context.Saga.CorrelationId, context.Saga.Flavor, context.Saga.Size);

        await context.Publish<PourShake>(new
        {
            context.Saga.OrderId,
            OrderLineId = context.Saga.CorrelationId,
            context.Saga.Flavor,
            context.Saga.Size
        }, x =>
        {
            x.RequestId = NewId.NextGuid();
            x.ResponseAddress = context.ReceiveContext.InputAddress;
        });
    }

    public Task Faulted<TException>(BehaviorExceptionContext<ShakeState, TException> context, IBehavior<ShakeState> next)
        where TException : Exception
        => next.Faulted(context);

    public Task Faulted<T, TException>(BehaviorExceptionContext<ShakeState, T, TException> context, IBehavior<ShakeState, T> next)
        where T : class
        where TException : Exception
        => next.Faulted(context);
}
