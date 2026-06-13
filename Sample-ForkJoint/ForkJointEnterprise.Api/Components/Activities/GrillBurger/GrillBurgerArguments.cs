namespace ForkJointEnterprise.Api.Components.Activities.GrillBurger;

public interface GrillBurgerArguments
{
    Guid OrderId { get; }
    Guid BurgerId { get; }

    decimal Weight { get; }
    bool Cheese { get; }
}