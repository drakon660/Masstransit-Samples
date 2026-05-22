using ForkJoint.Api.Components.Consumers;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddCarter();

builder.Services.AddMassTransit(x =>
{
    x.SetKebabCaseEndpointNameFormatter();

    x.AddConsumersFromNamespaceContaining<SubmitOrderConsumer>();
    x.AddActivitiesFromNamespaceContaining<ForkJoint.Api.Components.Activities.CourierActivities>();

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
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "ForkJoint API v1");
    });
}

app.UseHttpsRedirection();
app.MapCarter();

app.Run();
