using System.Runtime.CompilerServices;

namespace ForkJoint.Contracts;

public interface RequestBurger
{
    Guid OrderId { get; }

    Burger Burger { get; }

    [ModuleInitializer]
    internal static void Init()
    {
        GlobalTopology.Send.UseCorrelationId<RequestBurger>(x => x.Burger.BurgerId);
    }
}