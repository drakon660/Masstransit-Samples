namespace Library.Contracts;

public interface FineWaived
{
    Guid MemberId { get; }
    decimal Amount { get; }
    string Reason { get; }
}
