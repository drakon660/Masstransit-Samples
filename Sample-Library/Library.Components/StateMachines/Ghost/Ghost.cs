using MassTransit;

namespace Library.Components.StateMachines.Ghost;

public class Ghost : SagaStateMachineInstance
{
    public int CurrentState { get; set; }
    public Guid GhostId { get; set; }
    public Guid CorrelationId { get; set; }
}
