namespace Library.Contracts;

public interface FineCharged
{
    Guid MemberId { get; }
    decimal Amount { get; }
}