namespace ForkJointEnterprise.Api.Services;

public interface IShakeMachine
{
    Task PourShake(string flavor, Size size);
}
