namespace ForkJointEnterprise.Api.Components.StateMachines;

public class FryState :
    FutureState
{
    public Guid OrderId { get; set; }

    public Size Size { get; set; }

    public ExceptionInfo ExceptionInfo { get; set; } = null!;
}
