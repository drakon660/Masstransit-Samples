namespace Library.Contracts;

public interface BookAddedToMemberCollection
{
    Guid BookId { get; }
    Guid MemberId { get; }
}