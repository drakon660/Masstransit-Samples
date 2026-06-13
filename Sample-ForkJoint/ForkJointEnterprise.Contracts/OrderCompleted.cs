namespace ForkJointEnterprise.Contracts;

public interface OrderCompleted :
    FutureCompleted
{
    Guid OrderId { get; }

    IDictionary<Guid, OrderLineCompleted> LinesCompleted { get; }
}
