// =============================================================================
// Program.cs — Application entry point and composition root
//
// This file has two responsibilities:
//   1. SERVICE REGISTRATION (the "builder" phase): wire every dependency into
//      the DI container before the application starts handling requests.
//   2. MIDDLEWARE PIPELINE (the "app" phase): define the ordered chain of
//      components that every HTTP request passes through.
//
// Architectural rule: only infrastructure-level concerns live here.
//   Business logic belongs in DMS.Application.
//   Persistence and external services belong in DMS.Infrastructure.
//   HTTP concerns (controllers, Swagger, middleware) belong here.
//   Each layer exposes a single AddXxx() extension method so this file stays
//   flat — it orchestrates, it does not configure internals.
//
// .NET 6+ minimal hosting model:
//   There is no Startup.cs. ConfigureServices() and Configure() are merged into
//   this single file using top-level statements. The implicit entry point is the
//   compiler-generated Main() method — no class or namespace is needed.
// =============================================================================

using DMS.Api.Middleware;
using DMS.Application;
using DMS.Infrastructure;
using Serilog;

// WebApplication.CreateBuilder() bootstraps the host: loads configuration
// (appsettings.json → appsettings.{Environment}.json → environment variables),
// sets up the default DI container, and prepares the Kestrel web server.
// "args" passes through any command-line arguments (e.g., --urls, --environment).
var builder = WebApplication.CreateBuilder(args);

// -----------------------------------------------------------------------------
// Serilog structured logging
//
// Why replace the default Microsoft.Extensions.Logging with Serilog?
//   The default logger writes plain text. Serilog writes structured events —
//   each log entry is a set of typed key-value properties that log aggregation
//   tools (Seq, Elastic, Datadog) can index and query. For example, filtering
//   all logs where UserId = "abc" is a direct index lookup, not a string search.
//
// ReadFrom.Configuration() binds the entire Serilog section from appsettings.json:
//   - MinimumLevel (default + per-namespace overrides)
//   - Sinks (Console, File, Seq, etc.)
//   - Enrichers (FromLogContext, WithMachineName, etc.)
//   Changing log behaviour requires only an appsettings change — no recompile.
//
// Why UseSerilog() on Host rather than Services?
//   Serilog replaces the entire logging pipeline at the host level, not just
//   registers an ILogger implementation. Host-level registration ensures Serilog
//   captures startup logs (DI registration, configuration loading) that fire
//   before the request pipeline is built.
// -----------------------------------------------------------------------------
builder.Host.UseSerilog((context, config) =>
    config.ReadFrom.Configuration(context.Configuration));

// -----------------------------------------------------------------------------
// MVC controllers
//
// AddControllers() registers:
//   - IControllerActivator (creates controller instances from DI)
//   - Model binding (maps request data to action parameters)
//   - Input/output formatters (JSON serialisation via System.Text.Json by default)
//   - Data annotations validation ([Required], [MaxLength], etc.)
//   - Filter pipeline ([Authorize], [ValidateAntiForgeryToken], etc.)
//
// AddEndpointsApiExplorer() is required for Swagger/Swashbuckle to discover
// controller endpoints and generate the OpenAPI document. Without it, swagger.json
// is generated but contains no paths.
// -----------------------------------------------------------------------------
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

// -----------------------------------------------------------------------------
// Swagger / OpenAPI document generation
//
// SwaggerGen generates a swagger.json document at runtime by reflecting over
// all registered controller actions and their attributes ([HttpGet], [ProducesResponseType],
// XML doc comments, etc.).
//
// SwaggerDoc("v1", ...) defines the top-level API metadata block in swagger.json:
//   { "info": { "title": "Document Management Service", "version": "v1" } }
//
// OperationFilter<UserIdHeaderOperationFilter>() injects the X-User-Id header
// into every endpoint's parameter list in the generated document. This is
// necessary because the header is read from Request.Headers inside the action
// body — Swashbuckle cannot discover it automatically from the method signature.
// See DMS.Api/Swagger/UserIdHeaderOperationFilter.cs for full explanation.
//
// Level 1 note: this filter is replaced by a global Bearer security requirement
// when JWT authentication is introduced. The filter definition is deleted and
// options.AddSecurityDefinition / AddSecurityRequirement take its place.
// -----------------------------------------------------------------------------
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new() { Title = "Document Management Service", Version = "v1" });
    options.OperationFilter<DMS.Api.Swagger.UserIdHeaderOperationFilter>();
});

// -----------------------------------------------------------------------------
// Application and Infrastructure layer registration
//
// Each layer exposes a single extension method on IServiceCollection so this
// file stays flat. Internals of each layer (which repositories, which handlers,
// which services) are encapsulated within that layer's DependencyInjection.cs.
//
// AddApplication() — DMS.Application:
//   Registers MediatR with all IRequestHandler<,> implementations in the assembly.
//   (CQRS: every command and query is handled via MediatR dispatch.)
//
// AddInfrastructure(configuration) — DMS.Infrastructure:
//   Registers EF Core DbContext (SQL Server connection string from configuration),
//   repository implementations, and the local file storage service.
//   configuration is passed explicitly because Infrastructure is the only layer
//   allowed to read raw IConfiguration — Application and Domain must not.
// -----------------------------------------------------------------------------
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

// -----------------------------------------------------------------------------
// Health checks
//
// What health checks do:
//   GET /health returns 200 Healthy or 503 Unhealthy based on the registered
//   checks. Kubernetes liveness/readiness probes, load balancers, and uptime
//   monitors hit this endpoint to determine if the instance should receive traffic.
//
// AddSqlServer() probes the database with a lightweight query ("SELECT 1").
//   If the database is unreachable or the connection string is wrong, the health
//   endpoint returns 503, signalling the orchestrator to stop routing traffic
//   to this instance until the dependency recovers.
//
// The ! (null-forgiving) operator: GetConnectionString() returns string? but
// the database is required for the app to function. If it's null, the crash at
// startup is correct — there is nothing useful the app can do without a DB.
// -----------------------------------------------------------------------------
builder.Services.AddHealthChecks()
    .AddSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")!);

// Build() finalises the DI container and creates the WebApplication.
// No services can be registered after this point.
var app = builder.Build();

// =============================================================================
// Middleware pipeline
//
// Order is critical. Each User call adds a component to the pipeline; every
// incoming request flows through these components top-to-bottom, and the
// response flows back bottom-to-top.
//
// Current pipeline order:
//   1. GlobalExceptionMiddleware   — outermost catch-all; wraps everything below
//   2. Swagger (dev only)          — serves swagger.json and Swagger UI
//   3. HttpsRedirection            — redirects HTTP → HTTPS
//   4. Controllers (routing)       — routes requests to controller actions
//   5. Health checks               — lightweight /health endpoint
// =============================================================================

// Must be registered first so it wraps every other middleware in a try/catch.
// Any unhandled exception from any layer below returns a 500 ProblemDetails
// response instead of crashing the server or leaking a stack trace.
// See DMS.Api/Middleware/GlobalExceptionMiddleware.cs for full explanation.
app.UseMiddleware<GlobalExceptionMiddleware>();

// Swagger UI is only exposed in Development to avoid leaking API documentation
// to the public internet in production. In a deployed environment, Swagger
// should be disabled entirely or protected behind authentication.
if (app.Environment.IsDevelopment())
{
    // UseSwagger() serves the raw OpenAPI document at /swagger/v1/swagger.json
    app.UseSwagger();
    // UseSwaggerUI() serves the interactive browser UI at /swagger/index.html
    app.UseSwaggerUI();
}

// Redirects plain HTTP requests to HTTPS. Harmless in development when the
// app runs on HTTP, but essential in production to enforce transport security.
app.UseHttpsRedirection();

// Maps incoming requests to controller actions based on route templates
// ([Route], [HttpGet("{id}")], etc.) discovered during AddControllers().
app.MapControllers();

// Maps GET /health to the registered health checks.
// Kept separate from MapControllers() because health checks are not MVC
// controllers — they use the minimal endpoint routing system directly.
app.MapHealthChecks("/health");

// Starts the Kestrel web server and blocks until the application is shut down
// (Ctrl+C, SIGTERM, or IHostLifetime cancellation).
app.Run();
