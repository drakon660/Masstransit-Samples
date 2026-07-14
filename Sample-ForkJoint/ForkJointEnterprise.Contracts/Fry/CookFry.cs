namespace ForkJointEnterprise.Contracts;

public interface CookFry
{
    Guid OrderId { get; }
    Guid OrderLineId { get; }
    Size Size { get; }
}
