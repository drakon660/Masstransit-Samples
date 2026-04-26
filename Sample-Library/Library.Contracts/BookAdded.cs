namespace Library.Contracts;

public interface BookAdded
{
    Guid BookId { get; }
    string Title { get; }
    string Isbn { get; }
    DateTime Timestamp { get; }
}