namespace ForkJointEnterprise.Api.Components.StateMachines;

public class ShakeState :
    FutureState
{
    public Guid OrderId { get; set; }

    public string Flavor { get; set; } = null!;
    public Size Size { get; set; }

    public ExceptionInfo ExceptionInfo { get; set; } = null!;
}
