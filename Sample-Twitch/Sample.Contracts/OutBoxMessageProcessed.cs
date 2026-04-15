using System;

namespace Sample.Contracts;

public class OutBoxMessageProcessed
{
    public Guid Id { get; set; }
}
