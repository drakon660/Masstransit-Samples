namespace ForkJoint.Api.Components.Activities.DressBurger;

public interface DressBurgerArguments
{
    Guid OrderId { get; }

    BurgerPatty Patty { get; }

    bool Lettuce { get; }
    bool Pickles { get; }
    bool Ketchup { get; }
}