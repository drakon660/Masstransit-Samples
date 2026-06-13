namespace ForkJoint.Contracts;

public interface BurgerCompleted
{
    Guid OrderId { get; }

    Burger Burger { get; }
}