namespace Library.Contracts;

public interface BookCheckedOut
{
    DateTime Timestamp { get; }
    Guid MemberId { get; }
    Guid BookId { get; }
    Guid CheckOutId { get; }
}

public interface RenewCheckOut
{
    Guid CheckOutId { get;  }
    DateTime Timestamp { get; }
    Guid MemberId { get; }
    Guid BookId { get; }
}

public interface CheckOutRenewed
{
    Guid CheckOutId { get;  }
    DateTime Timestamp { get; }
    Guid MemberId { get; }
    Guid BookId { get; }
}