namespace ForkJointEnterprise.Api.Components.Activities.DressBurger;

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
        _logger.LogInformation("Dressing Burger: {OrderId} {Ketchup} {Lettuce}", context.Arguments.OrderId, context.Arguments.Ketchup,
            context.Arguments.Lettuce);

        if (context.Arguments.Lettuce)
            throw new InvalidOperationException("No lettuce available");
        
        await Task.Delay(1000);

        return context.Completed();
    }
}