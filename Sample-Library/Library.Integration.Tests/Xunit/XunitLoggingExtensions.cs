using Microsoft.Extensions.DependencyInjection;

namespace Library.Integration.Tests.Xunit;

internal static class XunitLoggingExtensions
{
    public static IServiceCollection UseSharedXunitLogging(this IServiceCollection services) =>
        services
            .AddLogging()
            .AddSingleton(SharedXunitLoggerFactory.Instance);
}
