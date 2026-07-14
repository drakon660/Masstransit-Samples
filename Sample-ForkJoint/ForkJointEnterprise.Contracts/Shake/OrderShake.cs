namespace ForkJointEnterprise.Contracts;

public interface OrderShake
{
    Guid OrderId { get; }
    Guid OrderLineId { get; }
    string Flavor { get; }
    Size Size { get; }
}
