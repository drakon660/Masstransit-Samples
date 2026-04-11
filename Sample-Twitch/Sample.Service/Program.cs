using System;
using MassTransit;
using MassTransit.MongoDbIntegration.MessageData;
using MassTransit.RabbitMqTransport;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Sample.Components.Consumers;
using Sample.Components.CourierActivities;
using Sample.Components.StateMachines;
using Sample.Components.StateMachines.OrderStateMachineActivities;
using Microsoft.FeatureManagement;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Sample.Components.BatchConsumers;
using Sample.Infrastructure;
using Serilog;
using Serilog.Events;
using Warehouse.Contracts;

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
    .Enrich.WithProperty("Service", "Sample.Service")
    .WriteTo.Console()
    .WriteTo.Seq("http://localhost:5341"));

builder.Services.AddOpenTelemetry()
    .ConfigureResource(r => r.AddService("Sample.Service"))
    .WithTracing(tracing =>
    {
        tracing.AddSource("MassTransit");
        tracing.AddOtlpExporter();
    });

builder.Services.AddScoped<AcceptOrderActivity>();
builder.Services.AddScoped<AllocateInventoryActivity>();

var mongoConnectionString = builder.Configuration.GetConnectionString("MongoDb");
var useAzureServiceBus = await builder.Configuration.IsFeatureEnabledAsync(FeatureFlags.UseAzureServiceBus);

builder.Services.AddFeatureManagement();
builder.Services.TryAddSingleton(KebabCaseEndpointNameFormatter.Instance);
builder.Services.AddMassTransit(cfg =>
{
    cfg.DisableUsageTelemetry();
    cfg.AddConsumersFromNamespaceContaining<SubmitOrderConsumer>();
    cfg.AddActivitiesFromNamespaceContaining<AllocateInventoryActivity>();
    cfg.AddSagaStateMachine<OrderStateMachine, OrderState>(typeof(OrderStateMachineDefinition))
        .MongoDbRepository(r =>
        {
            r.Connection = mongoConnectionString;
            r.DatabaseName = "orders";
        });

    cfg.AddConsumer<RoutingSlipBatchEventConsumer, RoutingSlipBatchEventConsumerDefinition>();

    if (useAzureServiceBus)
    {
        cfg.UsingAzureServiceBus((context, configurator) =>
        {
            configurator.Host(builder.Configuration.GetConnectionString("AzureServiceBus"));
            configurator.UseMessageData(new MongoDbMessageDataRepository(mongoConnectionString, "attachments"));
            configurator.ConfigureEndpoints(context);
        });
    }
    else
    {
        cfg.UsingRabbitMq((context, configurator) =>
        {
            configurator.Host(new Uri(builder.Configuration.GetConnectionString("RabbitMQ")));
            configurator.UseMessageData(new MongoDbMessageDataRepository(mongoConnectionString, "attachments"));
            configurator.UseMessageScheduler(new Uri("queue:quartz"));
            configurator.ConfigureEndpoints(context);
        });
    }

    cfg.AddRequestClient<AllocateInventory>();
});

var app = builder.Build();
await app.RunAsync();

Log.CloseAndFlush();
