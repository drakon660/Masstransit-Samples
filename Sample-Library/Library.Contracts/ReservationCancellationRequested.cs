namespace Library.Contracts;

public interface ReservationCancellationRequested
{
    Guid ReservationId { get; }

    DateTime Timestamp { get; }
}