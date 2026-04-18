namespace Library.Contracts;

public interface BookAdded
{
    Guid BookId { get; set; }
    string Title { get; set; }
    string Isbn { get; set; }
    DateTime Timestamp { get; }
}