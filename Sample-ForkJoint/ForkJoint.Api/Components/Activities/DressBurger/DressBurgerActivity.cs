namespace ForkJoint.Api.Components.Activities.DressBurger;

public class DressBurgerActivity :
    IExecuteActivity<DressBurgerArguments>
{
    readonly ILogger<DressBurgerActivity> _logger;

    public DressBurgerActivity(ILogger<DressBurgerActivity> logger)
    {
        _logger = logger;
    }

    public async Task<ExecutionResult> Execute(ExecuteContext<DressBurgerArguments> context)
    {
        _logger.LogInformation("Dressing Burger: {OrderId} {Ketchup}", context.Arguments.OrderId, context.Arguments.Ketchup);

        await Task.Delay(1000);

        return context.Completed();
    }
}