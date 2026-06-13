namespace ForkJointEnterprise.Api.Services;

public class ShakeMachine :
    IShakeMachine
{
    public async Task PourShake(string flavor, Size size)
    {
        await Task.Delay(1000);
    }
}
