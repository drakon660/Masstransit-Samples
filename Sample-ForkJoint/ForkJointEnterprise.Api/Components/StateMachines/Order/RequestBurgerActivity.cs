namespace ForkJointEnterprise.Api.Components.StateMachines.Order;

public class RequestBurgerActivity :
    IStateMachineActivity<OrderState, SubmitOrder>
{
    readonly ILogger<RequestBurgerActivity> _logger;

    public RequestBurgerActivity(ILogger<RequestBurgerActivity> logger)
    {
        _logger = logger;
    }

    public void Probe(ProbeContext context) => context.CreateScope("requestBurger");

    public void Accept(StateMachineVisitor visitor) => visitor.Visit(this);

    public async Task Execute(BehaviorContext<OrderState, SubmitOrder> context, IBehavior<OrderState, SubmitOrder> next)
    {
        _logger.LogInformation("RequestBurgerActivity fan-out: OrderId={OrderId} BurgerCount={Count}",
            context.Message.OrderId, context.Message.Burgers?.Length ?? 0);

        foreach (var burger in context.Message.Burgers)
        {
            _logger.LogInformation("RequestBurgerActivity publishing OrderBurger: OrderId={OrderId} BurgerId={BurgerId}",
                context.Message.OrderId, burger.BurgerId);

            await context.Publish<OrderBurger>(new
            {
                context.Message.OrderId,
                Burger = burger,
                __RequestId = InVar.Id,
                __ResponseAddress = context.ReceiveContext.InputAddress
            });
        }

        await next.Execute(context);
    }

    public Task Faulted<TException>(BehaviorExceptionContext<OrderState, SubmitOrder, TException> context, IBehavior<OrderState, SubmitOrder> next)
        where TException : Exception
    {
        _logger.LogError(context.Exception, "RequestBurgerActivity faulted: OrderId={OrderId}", context.Message.OrderId);
        return next.Faulted(context);
    }
}
