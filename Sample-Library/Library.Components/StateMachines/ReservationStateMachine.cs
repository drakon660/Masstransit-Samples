using Library.Contracts;
using MassTransit;

namespace Library.Components.StateMachines;

public class ReservationStateMachine : MassTransitStateMachine<Reservation>
{
    public ReservationStateMachine()
    {
        Event(() => ReservationRequested, x 
            => x.CorrelateById(m => m.Message.ReservationId));
        
        InstanceState(x => x.CurrentState, Requested);
        Initially(
            When(ReservationRequested).Then(UpdateSagaFromMessage)
                .TransitionTo(Requested));
        
        During(Requested,When(BookReserved)
            .Then((context) =>
            {
                context.Saga.Reserved = context.Message.Timestamp;
            }) 
            .TransitionTo(Reserved));
    }

    public State Requested { get; set; }
    public State Reserved { get; set; }

    public Event<ReservationRequested> ReservationRequested { get; set; }
    public Event<BookReserved> BookReserved { get; set; }
    
    private void UpdateSagaFromMessage(BehaviorContext<Reservation, ReservationRequested> saga)
    {
        saga.Saga.Created = saga.Message.Timestamp;
        saga.Saga.MemberId = saga.Message.MemberId;
        saga.Saga.BookId = saga.Message.BookId;
    }
}