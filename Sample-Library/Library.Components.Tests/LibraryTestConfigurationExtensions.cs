using MassTransit;
using Microsoft.Extensions.DependencyInjection;
using Quartz;

namespace Library.Components.Tests;

public static class LibraryTestConfigurationExtensions
{
    public static IServiceCollection ConfigureMassTransit(this IServiceCollection services, Action<IBusRegistrationConfigurator>? configure = null)
    {
        services.AddQuartz()
            .AddMassTransitTestHarness(x =>
            {
                x.SetKebabCaseEndpointNameFormatter();

                x.AddQuartzConsumers();

                x.AddPublishMessageScheduler();

                configure?.Invoke(x);

                x.UsingInMemory((context, cfg) =>
                {
                    cfg.UsePublishMessageScheduler();
                        
                    cfg.ConfigureEndpoints(context);
                });
            });
            
        return services;
    } 
}