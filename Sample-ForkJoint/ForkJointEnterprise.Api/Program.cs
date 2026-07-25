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

builder.Services.AddForkJointEnterpriseServices();

builder.Services.AddMassTransit(x =>
{
    x.DisableUsageTelemetry();
    x.AddForkJointEnterpriseComponents();

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
