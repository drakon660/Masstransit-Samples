using System;
using MassTransit;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.FeatureManagement;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Sample.Infrastructure;
using Serilog;
using Serilog.Events;
using Warehouse.Components.Consumers;
using Warehouse.Components.StateMachines;

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Information)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .CreateBootstrapLogger();

BsonSerializer.RegisterSerializer(new GuidSerializer(GuidRepresentation.Standard));

var builder = Host.CreateApplicationBuilder(args);

builder.Configuration.AddJsonFile("appsettings.json", true);
builder.Configuration.AddEnvironmentVariables();
builder.Configuration.AddCommandLine(args);

builder.Services.AddSerilog((services, lc) => lc
    .ReadFrom.Configuration(builder.Configuration)
    .ReadFrom.Services(services)
    .Enrich.FromLogContext()
    .Enrich.WithProperty("Service", "Warehouse.Service")
    .WriteTo.Console()
    .WriteTo.Seq("http://localhost:5341"));

builder.Services.AddOpenTelemetry()
    .ConfigureResource(r => r.AddService("Warehouse.Service"))
    .WithTracing(tracing =>
    {
        tracing.AddSource("MassTransit");
        tracing.AddOtlpExporter();
    });

var mongoConnectionString = builder.Configuration.GetConnectionString("MongoDb");
var useAzureServiceBus = await builder.Configuration.IsFeatureEnabledAsync(FeatureFlags.UseAzureServiceBus);

builder.Services.AddFeatureManagement();
builder.Services.TryAddSingleton(KebabCaseEndpointNameFormatter.Instance);
builder.Services.AddMassTransit(cfg =>
{
    cfg.DisableUsageTelemetry();
    cfg.AddConsumersFromNamespaceContaining<AllocateInventoryConsumer>();
    cfg.AddSagaStateMachine<AllocationStateMachine, AllocationState>(typeof(AllocateStateMachineDefinition))
        .MongoDbRepository(r =>
        {
            r.Connection = mongoConnectionString;
            r.DatabaseName = "allocations";
        });

    if (useAzureServiceBus)
    {
        cfg.UsingAzureServiceBus((context, configurator) =>
        {
            configurator.Host(builder.Configuration.GetConnectionString("AzureServiceBus"));
            configurator.ConfigureEndpoints(context);
        });
    }
    else
    {
        cfg.UsingRabbitMq((context, configurator) =>
        {
            configurator.Host(builder.Configuration.GetConnectionString("RabbitMQ"));
            configurator.UseMessageScheduler(new Uri("queue:quartz"));
            configurator.ConfigureEndpoints(context);
        });
    }
});

var app = builder.Build();
await app.RunAsync();

Log.CloseAndFlush();
