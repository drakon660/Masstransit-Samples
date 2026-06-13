namespace ForkJointEnterprise.Api.Components.StateMachines;

public class BurgerState :
    FutureState
{
    public Guid OrderId { get; set; }

    public Burger Burger { get; set; }

    public Guid TrackingNumber { get; set; }

    public ExceptionInfo ExceptionInfo { get; set; }
}