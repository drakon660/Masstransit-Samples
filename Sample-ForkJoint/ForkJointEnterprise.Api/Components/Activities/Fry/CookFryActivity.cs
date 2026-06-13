namespace ForkJointEnterprise.Api.Components.StateMachines;

public class CookFryActivity(ILogger<CookFryActivity> logger) :
    IStateMachineActivity<FryState>
{
    public void Probe(ProbeContext context) => context.CreateScope("cookFry");

    public void Accept(StateMachineVisitor visitor) => visitor.Visit(this);

    public async Task Execute(BehaviorContext<FryState> context, IBehavior<FryState> next)
    {
        await ExecuteCore(context);
        await next.Execute(context);
    }

    public async Task Execute<T>(BehaviorContext<FryState, T> context, IBehavior<FryState, T> next) where T : class
    {
        await ExecuteCore(context);
        await next.Execute(context);
    }

    async Task ExecuteCore(BehaviorContext<FryState> context)
    {
        logger.LogInformation("CookFry publishing: OrderId={OrderId} LineId={LineId} Size={Size}",
            context.Saga.OrderId, context.Saga.CorrelationId, context.Saga.Size);

        await context.Publish<CookFry>(new
        {
            context.Saga.OrderId,
            OrderLineId = context.Saga.CorrelationId,
            context.Saga.Size
        }, x =>
        {
            x.RequestId = NewId.NextGuid();
            x.ResponseAddress = context.ReceiveContext.InputAddress;
        });
    }

    public Task Faulted<TException>(BehaviorExceptionContext<FryState, TException> context, IBehavior<FryState> next)
        where TException : Exception
        => next.Faulted(context);

    public Task Faulted<T, TException>(BehaviorExceptionContext<FryState, T, TException> context, IBehavior<FryState, T> next)
        where T : class
        where TException : Exception
        => next.Faulted(context);
}
