namespace ForkJoint.Contracts;

public interface RequestBurger
{
    Guid OrderId { get; }

    Burger Burger { get; }
}