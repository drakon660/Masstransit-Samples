namespace ForkJointEnterprise.Api.Services;

public interface IFryer
{
    Task CookOnionRings(int quantity);
    Task CookFry(Size size);
}
