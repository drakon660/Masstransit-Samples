namespace Library.Contracts;

public interface AddBookToMemberCollection
{
    Guid BookId { get; }
    Guid MemberId { get; }
}