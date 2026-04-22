using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace DMS.Api.Swagger;

public sealed class UserIdHeaderOperationFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        operation.Parameters ??= [];

        operation.Parameters.Add(new OpenApiParameter
        {
            Name = "X-User-Id",
            In = ParameterLocation.Header,
            Required = false,
            Description = "The ID of the requesting user (required for all file operations)",
            Schema = new OpenApiSchema { Type = "string" }
        });
    }
}
