using System.Runtime.CompilerServices;

namespace ForkJoint.Contracts;

public interface OrderNotCompleted
{
    Guid OrderId { get; }

    string Reason { get; }

    Burger[] Burgers { get; }

    [ModuleInitializer]
    internal static void Init()
    {
        GlobalTopology.Send.UseCorrelationId<OrderNotCompleted>(x => x.OrderId);
    }
}