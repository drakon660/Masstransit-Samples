namespace ForkJointEnterprise.Contracts;

public interface OrderBurger
{
    Guid OrderId { get; }

    Burger Burger { get; }
}