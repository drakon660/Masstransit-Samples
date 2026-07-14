namespace ForkJointEnterprise.Contracts;

public interface OnionRingsCompleted :
    OrderLineCompleted
{
    int Quantity { get; }
}
