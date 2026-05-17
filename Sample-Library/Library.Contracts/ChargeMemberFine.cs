namespace Library.Contracts;

public interface ChargeMemberFine
{
    Guid MemberId { get; }
    decimal Amount { get; }
}