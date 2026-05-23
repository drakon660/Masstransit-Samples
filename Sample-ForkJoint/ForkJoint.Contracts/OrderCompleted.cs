using System.Runtime.CompilerServices;

namespace ForkJoint.Contracts;

public interface OrderCompleted
{
    Guid OrderId { get; }

    Burger Burger { get; }

    [ModuleInitializer]
    internal static void Init()
    {
        GlobalTopology.Send.UseCorrelationId<OrderCompleted>(x => x.OrderId);
    }
}