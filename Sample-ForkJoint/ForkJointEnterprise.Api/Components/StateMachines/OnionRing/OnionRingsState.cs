namespace ForkJointEnterprise.Api.Components.StateMachines;

public class OnionRingsState :
    FutureState
{
    public Guid OrderId { get; set; }

    public int Quantity { get; set; }

    public ExceptionInfo ExceptionInfo { get; set; } = null!;
}
