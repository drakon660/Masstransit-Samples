using ForkJoint.Api.Components.Activities.ItineraryPlanners;
using ForkJoint.Api.Components.Consumers;
using ForkJoint.Api.Components.StateMachines;
using ForkJoint.Api.Services;
using Microsoft.Extensions.DependencyInjection.Extensions;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddCarter();

builder.Services.TryAddScoped<IBurgerItineraryPlanner, BurgerItineraryPlanner>();
builder.Services.TryAddSingleton<IGrill, Grill>();

builder.Services.AddMassTransit(x =>
{
    x.DisableUsageTelemetry();
    
    x.SetKebabCaseEndpointNameFormatter();
    x.AddConsumersFromNamespaceContaining<SubmitOrderConsumer>();
    x.AddActivitiesFromNamespaceContaining<ForkJoint.Api.Components.Activities.CourierActivities>();
    x.AddExecuteActivity<DressBurgerActivity, DressBurgerArguments>();

    x.AddSagaStateMachine<BurgerStateMachine, BurgerState, BurgerSagaDefinition>()
        .InMemoryRepository();
    x.AddSagaStateMachine<MassTransit.Components.RequestStateMachine, MassTransit.Components.RequestState, RequestSagaDefinition>()
        .InMemoryRepository();
    
    x.AddRequestClient<SubmitOrder>();
    x.AddRequestClient<RequestBurger>();

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
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "ForkJoint API v1");
    });
}

app.UseHttpsRedirection();
app.MapCarter();

app.Run();
