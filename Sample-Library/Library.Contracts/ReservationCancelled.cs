namespace Library.Contracts;

public interface ReservationCancelled
{
    Guid ReservationId { get; set; }
    Guid BookId { get; set; }
}