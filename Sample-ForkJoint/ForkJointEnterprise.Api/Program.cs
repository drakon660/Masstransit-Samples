using ForkJointEnterprise.Api.Components.Activities.DressBurger;
using ForkJointEnterprise.Api.Components.Activities.GrillBurger;
using ForkJointEnterprise.Api.Components.Activities.ItineraryPlanners;
using ForkJointEnterprise.Api.Components.Consumers;
using ForkJointEnterprise.Api.Components.StateMachines;
using ForkJointEnterprise.Api.Services;
using Microsoft.Extensions.DependencyInjection.Extensions;

using ForkJointEnterprise.Api;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenTelemetry()
    .ConfigureResource(r => r.AddService("ForkJointEnterprise"))
    .WithTracing(tracing =>
    {
        tracing
            .AddSource("MassTransit")
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation()
            .AddOtlpExporter(o => o.Endpoint = new Uri("http://localhost:4317"));
    });

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SchemaFilter<UniqueGuidSchemaFilter>();
});
builder.Services.AddCarter();

builder.Services.TryAddScoped<IBurgerItineraryPlanner, BurgerItineraryPlanner>();
builder.Services.TryAddSingleton<IGrill, Grill>();
builder.Services.TryAddSingleton<IFryer, Fryer>();
builder.Services.TryAddSingleton<IShakeMachine, ShakeMachine>();

builder.Services.AddMassTransit(x =>
{
    x.DisableUsageTelemetry();
    x.SetEndpointNameFormatter(new KebabCaseEndpointNameFormatter("fork-joint-enterprise", false));

    // Activities
    x.AddActivity<GrillBurgerActivity, GrillBurgerArguments, GrillBurgerLog>();
    x.AddExecuteActivity<DressBurgerActivity, DressBurgerArguments>();

    // Consumers
    x.AddConsumer<CookFryConsumer>();
    x.AddConsumer<PourShakeConsumer>();
    x.AddConsumer<CookOnionRingsConsumer>();

    // Sagas
    x.AddSagaStateMachine<BurgerStateMachine, BurgerState, BurgerSagaDefinition>()
        .InMemoryRepository();

    x.AddSagaStateMachine<FryStateMachine, FryState, FrySagaDefinition>()
        .InMemoryRepository();

    x.AddSagaStateMachine<ShakeStateMachine, ShakeState, ShakeSagaDefinition>()
        .InMemoryRepository();

    x.AddSagaStateMachine<FryShakeStateMachine, FryShakeState, FryShakeSagaDefinition>()
        .InMemoryRepository();

    x.AddSagaStateMachine<OnionRingsStateMachine, OnionRingsState, OnionRingsSagaDefinition>()
        .InMemoryRepository();

    x.AddSagaStateMachine<OrderStateMachine, OrderState, OrderSagaDefinition>()
        .InMemoryRepository();

    // Bridges sagas using .RequestStarted() / .RequestCompleted() back to IRequestClient callers
    x.AddSagaStateMachine<MassTransit.Components.RequestStateMachine, MassTransit.Components.RequestState, RequestSagaDefinition>()
        .InMemoryRepository();

    // Request clients
    x.AddRequestClient<SubmitOrder>();
    x.AddRequestClient<OrderOnionRings>();

    x.UsingRabbitMq((context, cfg) =>
    {
        var host = builder.Configuration.GetConnectionString("RabbitMq") ?? "amqp://guest:guest@localhost:5672";
        cfg.Host(new Uri(host));

        cfg.ConfigureEndpoints(context);
    });
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "ForkJointEnterprise API v1");
    });
}

app.UseHttpsRedirection();
app.MapCarter();

app.Run();
