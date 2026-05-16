using Library.Components.StateMachines.Ghost;
using Library.Integration.Tests.Sagas;
using MassTransit;
using Meziantou.Extensions.Logging.Xunit.v3;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Library.Integration.Tests;

public static class LibraryIntegrationTestConfigurationExtensions
{
    public static IServiceCollection ConfigureMassTransitWithPostgres(
        this IServiceCollection services,
        string connectionString,
        Action<IBusRegistrationConfigurator> configure = null)
    {
        services.AddDbContext<LibrarySagaDbContext>(options =>
            options.UseNpgsql(connectionString, npg => npg.MigrationsAssembly(typeof(LibrarySagaDbContext).Assembly.GetName().Name)));

        services.AddMassTransitTestHarness(x =>
        {
            x.SetKebabCaseEndpointNameFormatter();

            configure?.Invoke(x);

            x.AddSagaStateMachine<GhostStateMachine, Ghost>()
                .EntityFrameworkRepository(r =>
                {
                    r.ConcurrencyMode = ConcurrencyMode.Pessimistic;
                    r.ExistingDbContext<LibrarySagaDbContext>();
                    r.UsePostgres();
                });

            x.UsingInMemory((context, cfg) => cfg.ConfigureEndpoints(context));
        });

        return services;
    }

    public static async Task<ServiceProvider> CreateProvider(string connectionString)
    {
        var services = new ServiceCollection();
        Library.Integration.Tests.Xunit.XunitLoggingExtensions.UseSharedXunitLogging(services)
            .ConfigureMassTransitWithPostgres(connectionString);

        var provider = services.BuildServiceProvider(true);

        await using var scope = provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<LibrarySagaDbContext>();
        await db.Database.EnsureCreatedAsync();

        return provider;
    }
}
