using Library.Contracts;
using MassTransit;

namespace Library.Components.StateMachines;

public class BookStateMachine : MassTransitStateMachine<Book>
{
    public BookStateMachine()
    {
        //GlobalTopology.Send.UseCorrelationId();
        //in some global static class initializer
        Event(() => Added, x => x.CorrelateById(m => m.Message.BookId));
        Event(() => ReservationRequested, x =>
            x.CorrelateById(m => m.Message.BookId));
        
        Event(() => ReservationCancelled, x =>
            x.CorrelateById(m => m.Message.BookId));
        
        Event(() => BookCheckedOut, x =>
            x.CorrelateById(m => m.Message.BookId));
        
        InstanceState(x => x.CurrentState);

        Initially(When(Added).Then(UpdateSagaFromMessage).TransitionTo(Available));

        During(Available, When(ReservationRequested).TransitionTo(Reserved).PublishBookReserved().TransitionTo(Reserved));
        
        During(Reserved,
            When(ReservationCancelled).TransitionTo(Available));
        
        During(Available,Reserved,
            When(BookCheckedOut)
            .TransitionTo(CheckedOut));
    }

    public State Available { get; set; }
    public State Reserved { get; set; }
    public State CheckedOut { get; set; }
    
    public Event<BookAdded> Added { get; set; }
    public Event<ReservationRequested> ReservationRequested { get; set; }
    public Event<BookReservationCancelled> ReservationCancelled { get; set; }
    public Event<BookCheckedOut> BookCheckedOut { get; set; }

    private void UpdateSagaFromMessage(BehaviorContext<Book, BookAdded> saga)
    {
        saga.Saga.DateAdded = saga.Message.Timestamp.Date;
        saga.Saga.CorrelationId = saga.Message.BookId;
        saga.Saga.Isbn = saga.Message.Isbn;
    }
}