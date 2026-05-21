using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Sample.Components.Tests.Xunit;

internal static class XunitLoggingExtensions
{
    public static IServiceCollection UseSharedXunitLogging(this IServiceCollection services) =>
        services
            .AddLogging()
            .AddSingleton(SharedXunitLoggerFactory.Instance);
}
