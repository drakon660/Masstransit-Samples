namespace ForkJoint.Contracts;

public interface BurgerNotCompleted
{
    Guid OrderId { get; }

    string Reason { get; }

    Burger Burger { get; }
}