using Library.Contracts;
using MassTransit;

namespace Library.Components.StateMachines;

public class ReservationStateMachine : MassTransitStateMachine<Reservation>
{
    public ReservationStateMachine()
    {
        Event(() => ReservationRequested, x 
            => x.CorrelateById(m => m.Message.ReservationId));
        Event(() => BookReserved, x 
            => x.CorrelateById(m => m.Message.ReservationId));
        
        // Schedule<Reservation, ReservationExpired> already registers ExpirationSchedule.Received
        // as the event for that message, so a separate Event(() => ReservationExpired, ...) is
        // redundant and can conflict. Use ExpirationSchedule.Received in behaviors instead.
        // Event(() => ReservationExpired, x
        //     => x.CorrelateById(m => m.Message.ReservationId));
        
        InstanceState(x => x.CurrentState, Requested, Reserved);
        
        Schedule(() => ExpirationSchedule, x => x.ExpirationTokenId, x =>
        {
            x.Delay = TimeSpan.FromHours(24);
            x.Received = r => 
                r.CorrelateById(m => m.Message.ReservationId);
        });
        
        Initially(
            When(ReservationRequested).Then(UpdateSagaFromMessage)
                .TransitionTo(Requested));
        
        During(Requested,
            When(BookReserved)
                .Then((context) =>
                {
                    context.Saga.Reserved = context.Message.Timestamp;
                }).Schedule(ExpirationSchedule, context => context.Init<ReservationExpired>(new
                {
                    context.Message.ReservationId
                }), context => context.Message.Duration ?? TimeSpan.FromDays(1))
                .TransitionTo(Reserved));

        During(Reserved,
            When(ExpirationSchedule.Received)
                .PublishAsync(ctx => ctx.Init<ReservationExpired>(new
                {
                    ReservationId = ctx.Saga.CorrelationId,
                    ctx.Saga.BookId
                }))
                .Finalize());
        
        SetCompletedWhenFinalized();
        //     
        // DuringAny(
        //     When(ExpirationSchedule.AnyReceived)
        //         .Finalize());
    }

    public State Requested { get; set; }
    public State Reserved { get; set; }
    public State Expired { get; set; }
    
    public Schedule<Reservation, ReservationExpired> ExpirationSchedule { get; set; }

    public Event<ReservationRequested> ReservationRequested { get; set; }
    public Event<BookReserved> BookReserved { get; set; }
    
    private void UpdateSagaFromMessage(BehaviorContext<Reservation, ReservationRequested> saga)
    {
        saga.Saga.Created = saga.Message.Timestamp;
        saga.Saga.MemberId = saga.Message.MemberId;
        saga.Saga.BookId = saga.Message.BookId;
    }
}