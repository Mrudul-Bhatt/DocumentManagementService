using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace DMS.Api.Swagger;

/// <summary>
/// Swashbuckle operation filter that injects the X-User-Id custom header into every
/// endpoint's parameter list in the generated OpenAPI document.
///
/// What problem this solves:
///   ASP.NET Core's Swagger/Swashbuckle generator only discovers parameters that are
///   declared explicitly on action method signatures (route params, query strings, body).
///   Custom HTTP headers read inside the action body via Request.Headers are invisible
///   to the generator — they never appear in the Swagger UI "Try it out" form.
///   This filter patches each generated OpenApiOperation at document-generation time
///   to add the header so testers can supply it directly from the UI.
///
/// How Swashbuckle operation filters work:
///   Swashbuckle calls IOperationFilter.Apply() once per discovered endpoint while
///   building the OpenAPI document. At that point, the filter can read or mutate the
///   OpenApiOperation object freely — adding parameters, security requirements,
///   response descriptions, etc. The result is written into the final swagger.json.
///   Registration in Program.cs: options.OperationFilter<UserIdHeaderOperationFilter>()
///
/// Why this class exists only in Level 0:
///   Level 0 uses a trusted X-User-Id header for caller identity — there is no
///   authentication layer. Level 1 replaces this with JWT Bearer authentication,
///   at which point this filter is deleted and replaced by a global security
///   requirement (Bearer token) defined once in Swagger configuration rather than
///   injected as a per-endpoint header parameter.
/// </summary>
public sealed class UserIdHeaderOperationFilter : IOperationFilter
{
    /// <summary>
    /// Adds the X-User-Id header parameter to the given OpenAPI operation.
    ///
    /// Called by Swashbuckle once per endpoint during OpenAPI document generation.
    /// The mutation here only affects the generated swagger.json — it has no effect
    /// on the actual ASP.NET Core request pipeline or runtime behaviour.
    /// </summary>
    /// <param name="operation">
    ///   The OpenAPI operation being built for a single controller action.
    ///   Mutating this object changes what appears in the Swagger UI for that endpoint.
    /// </param>
    /// <param name="context">
    ///   Metadata about the action method (MethodInfo, ApiDescription, schema registry).
    ///   Not used here because the header applies uniformly to every endpoint.
    ///   In more targeted filters, context is used to skip certain routes
    ///   (e.g., anonymous endpoints that don't need authentication headers).
    /// </param>
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        // Null-coalescing assignment: initialise the parameter list if Swashbuckle
        // hasn't created it yet (endpoints with no other parameters start with null).
        operation.Parameters ??= [];

        operation.Parameters.Add(new OpenApiParameter
        {
            Name = "X-User-Id",
            In = ParameterLocation.Header,   // Swagger UI renders this in the "Headers" section
            Required = false,                       // Marked optional so Swagger validation doesn't block
                                                    // requests; the controller enforces it at runtime.
            Description = "The ID of the requesting user (required for all file operations at runtime)",
            Schema = new OpenApiSchema { Type = "string" }
        });
    }
}
