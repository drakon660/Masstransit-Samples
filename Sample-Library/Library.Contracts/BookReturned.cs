namespace Library.Contracts;

public interface BookReturned
{
    Guid CheckOutId { get; }

    DateTime Timestamp { get; }

    Guid MemberId { get; }
    Guid BookId { get; }

    DateTime CheckOutDate { get; }
    DateTime DueDate { get; }
    DateTime ReturnDate { get; }
}