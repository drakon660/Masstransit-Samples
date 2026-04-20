namespace Library.Contracts;

public interface BookReservationCancelled
{
    Guid ReservationId { get; set; }
    Guid BookId { get; set; }
}
