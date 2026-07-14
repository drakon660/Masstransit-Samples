namespace ForkJointEnterprise.Contracts;

public interface OrderFry
{
    Guid OrderId { get; }
    Guid OrderLineId { get; }
    Size Size { get; }
}
