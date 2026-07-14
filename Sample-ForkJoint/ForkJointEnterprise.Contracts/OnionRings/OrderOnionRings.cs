namespace ForkJointEnterprise.Contracts;

public interface OrderOnionRings
{
    Guid OrderId { get; }
    Guid OrderLineId { get; }
    int Quantity { get; }
}
