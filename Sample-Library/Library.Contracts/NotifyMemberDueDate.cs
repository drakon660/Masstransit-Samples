namespace Library.Contracts;

public interface NotifyMemberDueDate
{
    Guid MemberId { get; }
    DateTime DueDate { get; }
}