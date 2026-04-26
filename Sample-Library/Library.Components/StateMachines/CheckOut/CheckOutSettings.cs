namespace Library.Components.StateMachines.CheckOut;

public interface CheckOutSettings
{
    TimeSpan CheckOutDuration { get; }
    TimeSpan CheckOutDurationLimit { get; }
}