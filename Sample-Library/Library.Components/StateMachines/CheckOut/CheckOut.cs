using MassTransit;

namespace Library.Components.StateMachines.CheckOut;

public class CheckOut : SagaStateMachineInstance
{
    public int CurrentState { get; set; }
    public Guid BookId { get; set; }

    public DateTime CheckoutDate { get; set; }
    public DateTime DueDate { get; set; }

    public Guid CorrelationId { get; set; }
    public Guid MemberId { get; set; }
}