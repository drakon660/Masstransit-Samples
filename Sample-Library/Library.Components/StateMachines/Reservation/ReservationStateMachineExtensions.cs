using Library.Contracts;
using MassTransit;

namespace Library.Components.StateMachines.Reservation;

public static class ReservationStateMachineExtensions
{
    public static EventActivityBinder<Reservation, T> PublishReservationCancelled<T>(this EventActivityBinder<Reservation, T> binder)
        where T : class
    {
        return binder.PublishAsync(context => context.Init<BookReservationCancelled>(new
        {
            ReservationId = context.Saga.CorrelationId,
            context.Saga.BookId
        }));
    }
}