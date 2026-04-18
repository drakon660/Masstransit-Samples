namespace Library.Contracts;

public interface ReservationRequested
{
    Guid ReservationId { get; set; }
    DateTime Timestamp { get; }
    Guid MemberId { get; set; }
    Guid BookId { get; set; }
}