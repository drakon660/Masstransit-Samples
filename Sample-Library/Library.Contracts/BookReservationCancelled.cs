namespace Library.Contracts;

public interface BookReservationCancelled
{
    Guid ReservationId { get; }
    Guid BookId { get; }
}
