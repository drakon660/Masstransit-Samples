namespace Sample.Infrastructure;

using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.FeatureManagement;

public static class FeatureFlagExtensions
{
    /// <summary>
    /// Evaluates a Microsoft.FeatureManagement flag at startup, before the host's
    /// service provider is built. Spins up a one-shot provider containing only
    /// the configuration and FeatureManagement so callers don't have to call
    /// <c>BuildServiceProvider()</c> on the host's main <see cref="IServiceCollection"/>
    /// (which would trip the ASP0000 analyzer).
    /// </summary>
    public static async Task<bool> IsFeatureEnabledAsync(this IConfiguration configuration, string featureName)
    {
        var services = new ServiceCollection();
        services.AddSingleton(configuration);
        services.AddFeatureManagement();

        await using var provider = services.BuildServiceProvider();
        var featureManager = provider.GetRequiredService<IFeatureManager>();
        return await featureManager.IsEnabledAsync(featureName);
    }
}
