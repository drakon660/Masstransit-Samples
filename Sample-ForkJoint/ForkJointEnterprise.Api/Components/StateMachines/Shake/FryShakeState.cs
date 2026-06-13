namespace ForkJointEnterprise.Api.Components.StateMachines;

public class FryShakeState :
    FutureState
{
    public Guid OrderId { get; set; }

    public string Flavor { get; set; } = null!;
    public Size Size { get; set; }

    public int BothState { get; set; }

    public ExceptionInfo ExceptionInfo { get; set; } = null!;
}
