namespace Library.Contracts;

public interface ChargeMemberFine
{
    Guid MemberId { get; }
    int MemberAge { get; }
    decimal Amount { get; }
}
