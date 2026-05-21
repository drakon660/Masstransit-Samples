using MassTransit;
using Microsoft.Extensions.DependencyInjection;
using Sample.Components.Tests.Xunit;

namespace Sample.Components.Tests;

public static partial class LibraryTestConfigurationExtensions
{
    public static IServiceCollection ConfigureMassTransit(this IServiceCollection services, Action<IBusRegistrationConfigurator>? configure = null)
    {
        services
            .AddMassTransitTestHarness(x =>
            {
                x.SetKebabCaseEndpointNameFormatter();
                x.UseSharedXunitLogging();

                x.AddPublishMessageScheduler();

                configure?.Invoke(x);

                x.UsingInMemory((context, cfg) =>
                {
                    cfg.ConfigureEndpoints(context);
                });
            });

        return services;
    }
}
