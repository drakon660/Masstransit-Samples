namespace Library.Contracts;

public interface GhostPinged
{
    Guid GhostId { get; }
    DateTime Timestamp { get; }
}
