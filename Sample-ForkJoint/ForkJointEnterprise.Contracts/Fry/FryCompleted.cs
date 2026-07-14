namespace ForkJointEnterprise.Contracts;

public interface FryCompleted :
    OrderLineCompleted
{
    Size Size { get; }
}
