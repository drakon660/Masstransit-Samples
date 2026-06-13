namespace ForkJoint.Contracts;

public interface OrderCompleted
{
    Guid OrderId { get; }

    Burger Burger { get; }
}