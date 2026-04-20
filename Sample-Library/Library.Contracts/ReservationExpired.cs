namespace Library.Contracts;

public interface ReservationExpired
{
    Guid ReservationId { get; }
    Guid BookId { get; }
}