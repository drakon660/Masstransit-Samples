using MassTransit;

namespace Library.Components.StateMachines;

public class Reservation : SagaStateMachineInstance
{
    public Guid CorrelationId { get; set; }
    public Guid MemberId { get; set; }
    public DateTime Created { get; set; }
    public DateTime? Reserved { get; set; }
    public int CurrentState { get; set; }
    public Guid BookId { get; set; }
}