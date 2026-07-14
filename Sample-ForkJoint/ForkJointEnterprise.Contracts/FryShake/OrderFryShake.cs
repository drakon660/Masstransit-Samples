namespace ForkJointEnterprise.Contracts;

public interface OrderFryShake
{
    Guid OrderId { get; }
    Guid OrderLineId { get; }
    string Flavor { get; }
    Size Size { get; }
}
