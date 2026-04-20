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
        Event(() => ReservationExpired, x =>
            x.CorrelateById(m => m.Message.BookId));
        
        InstanceState(x => x.CurrentState);

        Initially(When(Added).Then(UpdateSagaFromMessage).TransitionTo(Available));

        DuringAny(When(ReservationRequested).TransitionTo(Reserved).PublishAsync(context => context.Init<BookReserved>(
            new
            {
                ReservationId = context.Message.ReservationId,
                Timestamp = context.Message.Timestamp,
                MemberId = context.Message.MemberId,
                BookId = context.Message.BookId,
                context.Message.Duration,
            }
        )).TransitionTo(Reserved));
        
        During(Reserved,
            When(ReservationExpired).TransitionTo(Available));
    }

    public State Available { get; set; }
    public State Reserved { get; set; }
    public Event<BookAdded> Added { get; set; }
    public Event<ReservationRequested> ReservationRequested { get; set; }
    public Event<ReservationExpired> ReservationExpired { get; set; }

    private void UpdateSagaFromMessage(BehaviorContext<Book, BookAdded> saga)
    {
        saga.Saga.DateAdded = saga.Message.Timestamp.Date;
        saga.Saga.CorrelationId = saga.Message.BookId;
        saga.Saga.Isbn = saga.Message.Isbn;
    }
}