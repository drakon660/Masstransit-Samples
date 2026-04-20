namespace Library.Contracts;

public interface BookCheckedOut
{ 
    DateTime Timestamp { get; }
    Guid MemberId { get; set; }
    Guid BookId { get; set; }
}