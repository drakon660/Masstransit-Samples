namespace ForkJointEnterprise.Contracts;

public interface OrderLineCompleted :
    FutureCompleted
{
    Guid OrderId { get; }
    Guid OrderLineId { get; }
    string Description { get; }
}