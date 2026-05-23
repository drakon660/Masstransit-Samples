namespace ForkJoint.Api.Components.StateMachines;

public class BurgerState :
    SagaStateMachineInstance
{
    public int CurrentState { get; set; }

    public Guid OrderId { get; set; }

    public Burger Burger { get; set; }

    public Guid TrackingNumber { get; set; }

    public string Reason { get; set; }

    public Guid CorrelationId { get; set; }
}