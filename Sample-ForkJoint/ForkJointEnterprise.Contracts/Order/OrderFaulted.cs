namespace ForkJointEnterprise.Contracts;

public interface OrderFaulted :
    FutureFaulted
{
    Guid OrderId { get; }

    IDictionary<Guid, OrderLineCompleted> LinesCompleted { get; }

    IDictionary<Guid, OrderLineFaulted> LinesFaulted { get; }
}