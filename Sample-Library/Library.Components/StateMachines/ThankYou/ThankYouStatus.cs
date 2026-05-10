namespace Library.Components.StateMachines.ThankYou;

public interface ThankYouStatus
{
    Guid MemberId { get; }
    Guid BookId { get; }
    string Status { get; }
}