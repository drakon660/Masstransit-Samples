using System.Runtime.CompilerServices;

namespace ForkJointEnterprise.Contracts;

public interface SubmitOrder
{
    Guid OrderId { get; }
    Burger[] Burgers { get; }
}