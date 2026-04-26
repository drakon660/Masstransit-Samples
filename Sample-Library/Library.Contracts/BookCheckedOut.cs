namespace Library.Contracts;

public interface BookCheckedOut
{
    DateTime Timestamp { get; }
    Guid MemberId { get; }
    Guid BookId { get; }
    Guid CheckOutId { get; }
}