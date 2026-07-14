namespace ForkJointEnterprise.Contracts;

public interface SubmitOrder
{
    Guid OrderId { get; }
    Burger[] Burgers { get; }
    Fry[] Fries { get; }
    Shake[] Shakes { get; }
    FryShake[] FryShakes { get; }
}

public record Fry
{
    public Guid FryId { get; init; }
    public Size Size { get; init; }
}

public record Shake
{
    public Guid ShakeId { get; init; }
    public string Flavor { get; init; } = null!;
    public Size Size { get; init; }
}

public record FryShake
{
    public Guid FryShakeId { get; init; }
    public string Flavor { get; init; } = null!;
    public Size Size { get; init; }
}
