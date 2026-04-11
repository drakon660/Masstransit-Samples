namespace Sample.Contracts;

using System;


public interface ChangeCardNumber
{
    Guid OrderId { get; }
    string PaymentCardNumber { get; }
}