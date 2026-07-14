namespace ForkJointEnterprise.Api.Components.StateMachines;

public class CookOnionRingsActivity(ILogger<CookOnionRingsActivity> logger) :
    IStateMachineActivity<OnionRingsState>
{
    public void Probe(ProbeContext context) => context.CreateScope("cookOnionRings");

    public void Accept(StateMachineVisitor visitor) => visitor.Visit(this);

    public async Task Execute(BehaviorContext<OnionRingsState> context, IBehavior<OnionRingsState> next)
    {
        await ExecuteCore(context);
        await next.Execute(context);
    }

    public async Task Execute<T>(BehaviorContext<OnionRingsState, T> context, IBehavior<OnionRingsState, T> next) where T : class
    {
        await ExecuteCore(context);
        await next.Execute(context);
    }

    async Task ExecuteCore(BehaviorContext<OnionRingsState> context)
    {
        logger.LogInformation("CookOnionRings publishing: OrderId={OrderId} LineId={LineId} Quantity={Quantity}",
            context.Saga.OrderId, context.Saga.CorrelationId, context.Saga.Quantity);

        await context.Publish<CookOnionRings>(new
        {
            context.Saga.OrderId,
            OrderLineId = context.Saga.CorrelationId,
            context.Saga.Quantity
        }, x =>
        {
            x.RequestId = NewId.NextGuid();
            x.ResponseAddress = context.ReceiveContext.InputAddress;
        });
    }

    public Task Faulted<TException>(BehaviorExceptionContext<OnionRingsState, TException> context, IBehavior<OnionRingsState> next)
        where TException : Exception
        => next.Faulted(context);

    public Task Faulted<T, TException>(BehaviorExceptionContext<OnionRingsState, T, TException> context, IBehavior<OnionRingsState, T> next)
        where T : class
        where TException : Exception
        => next.Faulted(context);
}
