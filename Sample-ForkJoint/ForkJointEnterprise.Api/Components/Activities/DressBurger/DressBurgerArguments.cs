namespace ForkJointEnterprise.Api.Components.Activities.DressBurger;

public interface DressBurgerArguments
{
    Guid OrderId { get; }
    Guid BurgerId { get; }

    BurgerPatty Patty { get; }

    bool Lettuce { get; }
    bool Pickle { get; }
    bool Onion { get; }
    bool Ketchup { get; }
    bool Mustard { get; }
    bool BarbecueSauce { get; }
    bool OnionRing { get; }
}