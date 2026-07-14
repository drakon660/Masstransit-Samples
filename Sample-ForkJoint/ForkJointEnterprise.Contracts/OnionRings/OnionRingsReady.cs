namespace ForkJointEnterprise.Contracts;

public interface OnionRingsReady
{
    Guid OrderId { get; }
    Guid OrderLineId { get; }
    int Quantity { get; }
}
