using Meziantou.Extensions.Logging.Xunit.v3;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ForkJointEnterpriseTests;

internal static class SharedXunitLoggerFactory
{
    public static readonly ILoggerFactory Instance = LoggerFactory.Create(builder =>
    {
        builder.SetMinimumLevel(LogLevel.Debug);
        builder.AddFilter("MassTransit", LogLevel.Debug);
        builder.AddFilter("Quartz", LogLevel.None);
        builder.AddProvider(new XUnitLoggerProvider(TestOutputRelay.Shared, appendScope: true));
    });
}

internal sealed class TestOutputRelay : ITestOutputHelper
{
    private static readonly TestOutputRelay Instance = new();
    private static ITestOutputHelper _current;

    public static ITestOutputHelper Shared => Instance;

    public static void Use(ITestOutputHelper output)
    {
        _current = output;
    }

    public string Output => _current?.Output ?? string.Empty;

    public void Write(string message) => _current?.Write(message);

    public void Write(string format, params object[] args) => _current?.Write(format, args);

    public void WriteLine(string message) => _current?.WriteLine(message);

    public void WriteLine(string format, params object[] args) => _current?.WriteLine(format, args);
}

internal static class XunitLoggingExtensions
{
    public static IServiceCollection UseSharedXunitLogging(this IServiceCollection services) =>
        services
            .AddLogging()
            .AddSingleton(SharedXunitLoggerFactory.Instance);
}