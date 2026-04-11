namespace Sample.Infrastructure;

/// <summary>
/// Feature flag names consumed by the host projects via Microsoft.FeatureManagement.
/// Defined as constants so flag names stay in sync across services and typos surface
/// at compile time instead of as silently-disabled features.
/// </summary>
public static class FeatureFlags
{
    /// <summary>
    /// When enabled, hosts wire MassTransit to Azure Service Bus.
    /// When disabled (default), hosts use RabbitMQ.
    /// </summary>
    public const string UseAzureServiceBus = nameof(UseAzureServiceBus);
}
