using System.Runtime.CompilerServices;

namespace ForkJoint.Contracts;

public interface OrderSubmissionAccepted
{
    Guid OrderId { get; }

    [ModuleInitializer]
    internal static void Init()
    {
        GlobalTopology.Send.UseCorrelationId<OrderSubmissionAccepted>(x => x.OrderId);
    }
}