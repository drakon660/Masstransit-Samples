namespace ForkJointEnterprise.Contracts;

public interface BurgerCompleted :
    OrderLineCompleted
{
    Burger Burger { get; }
}