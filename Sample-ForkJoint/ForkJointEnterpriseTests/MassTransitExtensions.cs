using MassTransit;
using Microsoft.Extensions.DependencyInjection;

namespace ForkJointEnterpriseTests;

public static class MassTransitExtensions
{
    public static IServiceCollection ConfigureMassTransit(this IServiceCollection services, Action<IBusRegistrationConfigurator> configure = null)
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
                    cfg.UsePublishMessageScheduler();
                    cfg.ConfigureEndpoints(context);
                });
            });
            
        return services;
    } 
}