namespace ForkJoint.Api.Services;

public interface IGrill
{
    Task<BurgerPatty> CookOrUseExistingPatty(decimal weight, bool cheese);
    void Add(BurgerPatty patty);
}