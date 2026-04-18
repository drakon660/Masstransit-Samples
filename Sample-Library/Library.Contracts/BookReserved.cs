namespace Library.Contracts;

public interface BookReserved
{
    Guid ReservationId { get; set; }
    DateTime Timestamp { get; }
    Guid MemberId { get; set; }
    Guid BookId { get; set; }
}