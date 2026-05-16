using Meziantou.Extensions.Logging.Xunit.v3;
using Microsoft.Extensions.Logging;

namespace Library.Integration.Tests.Xunit;

internal static class SharedXunitLoggerFactory
{
    public static readonly ILoggerFactory Instance = LoggerFactory.Create(builder =>
    {
        builder.SetMinimumLevel(LogLevel.Debug);
        builder.AddFilter("Microsoft.EntityFrameworkCore", LogLevel.Information);
        builder.AddFilter("MassTransit", LogLevel.Debug);
        builder.AddProvider(new XUnitLoggerProvider(TestOutputRelay.Shared, appendScope: true));
    });
}
