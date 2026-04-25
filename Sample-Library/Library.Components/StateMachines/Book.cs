using MassTransit;

namespace Library.Components.StateMachines;

public class Book : SagaStateMachineInstance
{
    public int CurrentState { get; set; }
    public DateTime DateAdded { get; set; }
    public string Title { get; set; }
    public string Isbn { get; set; }
    
    public Guid? ReservationId { get; set; }
    public Guid CorrelationId { get; set; }
}
