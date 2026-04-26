namespace Library.Contracts;

public interface CheckOutRenewed
{
    Guid CheckOutId { get;  }
    DateTime DueDate { get; }
}