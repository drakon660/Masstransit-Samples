using Library.Contracts;
using MassTransit;

namespace Library.Components.StateMachines.Book;

public static class BookStateMachineExtensions
{
    public static EventActivityBinder<Book, ReservationRequested> PublishBookReserved(this EventActivityBinder<Book, ReservationRequested> binder)
    {
        return binder.PublishAsync(context => context.Init<BookReserved>(
            new
            {
                context.Message.ReservationId,
                context.Message.Timestamp,
                context.Message.MemberId,
                context.Message.BookId,
                context.Message.Duration,
            }));
    }
}