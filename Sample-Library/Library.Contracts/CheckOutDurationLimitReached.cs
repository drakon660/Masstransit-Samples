namespace Library.Contracts;

public interface CheckOutDurationLimitReached
{
    Guid CheckOutId { get;  }
    DateTime DueDate { get; }
}