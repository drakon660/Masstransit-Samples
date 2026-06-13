namespace ForkJointEnterprise.Contracts;

public interface OrderLineFaulted :
    FutureFaulted
{
    Guid OrderId { get; }
    Guid OrderLineId { get; }
    string Description { get; }
}