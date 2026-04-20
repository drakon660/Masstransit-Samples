namespace Library.Contracts;

public interface BookReserved
{
    Guid ReservationId { get; }
    DateTime Timestamp { get; }
    Guid MemberId { get; set; }
    Guid BookId { get; set; }
    TimeSpan? Duration { get; }
}