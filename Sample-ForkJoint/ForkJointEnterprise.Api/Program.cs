using ForkJointEnterprise.Api.Components.Activities.DressBurger;
using ForkJointEnterprise.Api.Components.Activities.GrillBurger;
using ForkJointEnterprise.Api.Components.Activities.ItineraryPlanners;
using ForkJointEnterprise.Api.Components.StateMachines;
using ForkJointEnterprise.Api.Services;
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
    x.SetEndpointNameFormatter(new KebabCaseEndpointNameFormatter("fork-joint-enterprise", false));

    // Activities
    x.AddActivity<GrillBurgerActivity, GrillBurgerArguments, GrillBurgerLog>();
    x.AddExecuteActivity<DressBurgerActivity, DressBurgerArguments>();

    // Sagas
    x.AddSagaStateMachine<BurgerStateMachine, BurgerState, BurgerSagaDefinition>()
        .InMemoryRepository();

    x.AddSagaStateMachine<OrderStateMachine, OrderState, OrderSagaDefinition>()
        .InMemoryRepository();

    // Bridges sagas using .RequestStarted() / .RequestCompleted() back to IRequestClient callers
    x.AddSagaStateMachine<MassTransit.Components.RequestStateMachine, MassTransit.Components.RequestState, RequestSagaDefinition>()
        .InMemoryRepository();

    // Request clients
    x.AddRequestClient<SubmitOrder>();

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
