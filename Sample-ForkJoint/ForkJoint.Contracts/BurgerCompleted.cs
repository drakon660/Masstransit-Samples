using System.Runtime.CompilerServices;

namespace ForkJoint.Contracts;

public interface BurgerCompleted
{
    Guid OrderId { get; }

    Burger Burger { get; }

    [ModuleInitializer]
    internal static void Init()
    {
        GlobalTopology.Send.UseCorrelationId<BurgerCompleted>(x => x.Burger.BurgerId);
    }
}