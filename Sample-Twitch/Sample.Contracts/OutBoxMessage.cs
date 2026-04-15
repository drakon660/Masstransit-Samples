using System;

namespace Sample.Contracts;

public class OutBoxMessage
{
    public Guid Id { get; set; }
}