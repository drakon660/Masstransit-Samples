using System;
using MassTransit;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.FeatureManagement;
using Sample.Components.Consumers;
using Sample.Contracts;
using Sample.Infrastructure;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;
using Serilog.Events;

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Information)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    Log.Information("Starting web host");

    var builder = WebApplication.CreateBuilder(args);
    builder.Services.AddSerilog((services, lc) => lc
        .ReadFrom.Configuration(builder.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext()
        .Enrich.WithProperty("Service", "Sample.Api")
        .WriteTo.Console()
        .WriteTo.Seq("http://localhost:5341"));
    
    builder.Services.AddHealthChecks();

    builder.Services.TryAddSingleton(KebabCaseEndpointNameFormatter.Instance);
    
    //EndpointConvention.Map<SubmitOrder>(new Uri($"queue:{KebabCaseEndpointNameFormatter.Instance.Consumer<SubmitOrderConsumer>()}"));
    
    var useAzureServiceBus = await builder.Configuration.IsFeatureEnabledAsync(FeatureFlags.UseAzureServiceBus);

    builder.Services.AddFeatureManagement();
    builder.Services.AddMassTransit(mt =>
    {
        mt.DisableUsageTelemetry();
        
        if (useAzureServiceBus)
        {
            mt.UsingAzureServiceBus((context, cfg) =>
            {
                cfg.Host(builder.Configuration.GetConnectionString("AzureServiceBus"));
            });
        }
        else
        {
            mt.UsingRabbitMq((context, cfg) =>
            {
                cfg.Host(builder.Configuration.GetConnectionString("RabbitMQ"));
                cfg.PurgeOnStartup = true;
            });
        }

        mt.AddRequestClient<SubmitOrder>(new Uri($"queue:{KebabCaseEndpointNameFormatter.Instance.Consumer<SubmitOrderConsumer>()}"));

        mt.AddRequestClient<CheckOrder>();
    });

    builder.Services.Configure<HealthCheckPublisherOptions>(options =>
    {
        options.Delay = TimeSpan.FromSeconds(2);
        options.Predicate = check => check.Tags.Contains("ready");
    });

    builder.Services.AddOpenTelemetry()
        .ConfigureResource(r => r.AddService("Sample.Api"))
        .WithTracing(tracing =>
        {
            tracing.AddAspNetCoreInstrumentation();
            tracing.AddHttpClientInstrumentation();
            tracing.AddSource("MassTransit");
            tracing.AddOtlpExporter();
        });

    builder.Services.AddOpenApi();
    builder.Services.AddControllers();

    var app = builder.Build();

    if (app.Environment.IsDevelopment())
        app.UseDeveloperExceptionPage();

    app.MapOpenApi();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/openapi/v1.json", "v1");
        options.HeadContent = """<style>html { color-scheme: light only !important; }</style>""";
    });

    app.UseRouting();
    app.UseAuthorization();

    app.MapControllers();

    app.MapHealthChecks("/health/ready", new HealthCheckOptions
    {
        Predicate = check => check.Tags.Contains("ready"),
    });

    app.MapHealthChecks("/health/live", new HealthCheckOptions
    {
        Predicate = _ => false
    });

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Host terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}
