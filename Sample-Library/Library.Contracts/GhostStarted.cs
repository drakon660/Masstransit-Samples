namespace Library.Contracts;

public interface GhostStarted
{
    Guid GhostId { get; }
    DateTime Timestamp { get; }
}
