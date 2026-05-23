using System.Runtime.CompilerServices;

namespace ForkJoint.Contracts;

public interface BurgerNotCompleted
{
    Guid OrderId { get; }

    string Reason { get; }

    Burger Burger { get; }

    [ModuleInitializer]
    internal static void Init()
    {
        GlobalTopology.Send.UseCorrelationId<BurgerNotCompleted>(x => x.Burger.BurgerId);
    }
}