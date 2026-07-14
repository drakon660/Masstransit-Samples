using System.Text.Json.Nodes;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace ForkJointEnterprise.Api;

public class UniqueGuidSchemaFilter : ISchemaFilter
{
    public void Apply(IOpenApiSchema schema, SchemaFilterContext context)
    {
        if (schema is not OpenApiSchema concreteSchema)
            return;

        if (context.Type == typeof(Guid) || context.Type == typeof(Guid?))
        {
            concreteSchema.Example = JsonValue.Create(NewId.NextGuid().ToString());
            return;
        }

        if (concreteSchema.Properties == null)
            return;

        foreach (var property in concreteSchema.Properties)
        {
            if (property.Value is not OpenApiSchema propSchema)
                continue;

            if (propSchema.Format == "uuid")
                propSchema.Example = JsonValue.Create(NewId.NextGuid().ToString());
            else if (propSchema.Type == JsonSchemaType.Boolean)
                propSchema.Default = JsonValue.Create(false);
        }
    }
}
