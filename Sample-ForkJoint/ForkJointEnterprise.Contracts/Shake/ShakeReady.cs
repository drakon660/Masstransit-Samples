namespace ForkJointEnterprise.Contracts;

public interface ShakeReady
{
    Guid OrderId { get; }
    Guid OrderLineId { get; }
    string Flavor { get; }
    Size Size { get; }
}
