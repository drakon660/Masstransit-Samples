namespace ForkJoint.Contracts;

public interface OrderNotCompleted
{
    Guid OrderId { get; }

    string Reason { get; }

    Burger[] Burgers { get; }
}