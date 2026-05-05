// =============================================================================
// Program.cs — Application entry point and composition root (Level 1)
//
// Level 1 additions over Level 0:
//   SERVICE REGISTRATION:
//     - JWT Bearer authentication (AddAuthentication / AddJwtBearer)
//     - Authorisation policy engine (AddAuthorization)
//     - Sliding window rate limiter on the login endpoint (AddRateLimiter)
//     - Swagger Bearer security definition replacing the X-User-Id header filter
//
//   MIDDLEWARE PIPELINE:
//     - UseRateLimiter()      — enforces per-policy request rate limits
//     - UseAuthentication()   — validates JWT Bearer tokens on incoming requests
//     - UseAuthorization()    — enforces [Authorize] attributes on endpoints
//     Both middleware are order-sensitive — see pipeline section below.
// =============================================================================

using System.Text;
using System.Threading.RateLimiting;
using DMS.Api.Middleware;
using DMS.Application;
using DMS.Infrastructure;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// Serilog replaces the default Microsoft.Extensions.Logging pipeline.
// ReadFrom.Configuration() binds the full Serilog section from appsettings.json
// (sinks, minimum levels, enrichers) so log behaviour is controlled by config,
// not code. See appsettings.json for the output template and level overrides.
builder.Host.UseSerilog((context, config) =>
    config.ReadFrom.Configuration(context.Configuration));

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

// -----------------------------------------------------------------------------
// Swagger / OpenAPI — Level 1: Bearer security definition
//
// Level 0 used UserIdHeaderOperationFilter to inject the X-User-Id header into
// every endpoint. Level 1 replaces that with a global Bearer security definition
// and requirement, which tells Swagger UI to show a single "Authorize" button
// that sets the Authorization: Bearer <token> header on all requests.
//
// AddSecurityDefinition("Bearer", ...) registers the scheme in the OpenAPI document.
// AddSecurityRequirement(...) marks every endpoint as requiring that scheme by default,
// so the padlock icon appears on all endpoints without per-endpoint annotation.
// The Reference ties the requirement back to the named definition "Bearer".
// -----------------------------------------------------------------------------
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new() { Title = "Document Management Service", Version = "v1" });

    var jwtScheme = new OpenApiSecurityScheme
    {
        Name        = "Authorization",
        Type        = SecuritySchemeType.Http,
        Scheme      = "bearer",        // lowercase per OpenAPI spec
        BearerFormat = "JWT",
        In          = ParameterLocation.Header,
        Description = "Enter your JWT access token."
    };
    options.AddSecurityDefinition("Bearer", jwtScheme);

    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                // Reference by the "Bearer" name registered above — links the
                // requirement to the definition without duplicating its properties.
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
            },
            Array.Empty<string>()   // empty scope list — no OAuth scopes needed for JWT
        }
    });
});

// -----------------------------------------------------------------------------
// JWT Bearer authentication
//
// AddAuthentication sets JWT Bearer as the default scheme — every [Authorize]
// attribute will use this scheme unless overridden.
//
// TokenValidationParameters controls what the middleware validates on every request:
//   ValidateIssuer / ValidateAudience — confirms the token was issued for this service.
//     Without these checks, a token from a different service (same signing key) would
//     be accepted, enabling token reuse across services.
//   ValidateLifetime — rejects expired tokens (checks the "exp" claim).
//   ValidateIssuerSigningKey — verifies the HMAC-SHA256 signature against the secret.
//     A tampered token's signature will not match — the request is rejected.
//   ClockSkew = TimeSpan.Zero — removes the default 5-minute leeway on token expiry.
//     The default exists to handle minor clock drift between servers. With Zero,
//     tokens expire exactly at their stated expiry time. This is stricter but
//     correct for a single-server deployment.
//
// Why SymmetricSecurityKey?
//   HMAC-SHA256 (symmetric) uses the same secret key to sign and verify tokens.
//   It is simpler than asymmetric (RS256/ES256) and sufficient when the API both
//   issues and verifies tokens. Asymmetric keys are needed when tokens are issued
//   by one service and verified by another (e.g., a dedicated auth server).
// -----------------------------------------------------------------------------
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        var jwtSecret = builder.Configuration["Jwt:Secret"]!;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer           = true,
            ValidateAudience         = true,
            ValidateLifetime         = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer              = builder.Configuration["Jwt:Issuer"],
            ValidAudience            = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey         = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)),
            ClockSkew                = TimeSpan.Zero
        };
    });

// Registers the authorisation policy engine. Required even when using only the
// default policy ([Authorize] with no named policy). Without this call,
// UseAuthorization() in the pipeline has no policy engine to delegate to and throws.
builder.Services.AddAuthorization();

// -----------------------------------------------------------------------------
// Rate limiting — sliding window on the login endpoint
//
// Why rate limit login specifically?
//   Login is the primary attack surface for credential stuffing (using known
//   username/password lists) and brute-force attacks. Capping attempts at 5 per
//   minute per IP makes automated attacks impractical without blocking legitimate users.
//
// Why a sliding window instead of a fixed window?
//   A fixed window resets at a hard boundary (e.g., every full minute), which means
//   an attacker can send 5 requests at 0:59 and 5 more at 1:00 — 10 requests in
//   2 seconds. A sliding window tracks requests over a rolling period, preventing
//   this burst at window boundaries.
//
// SegmentsPerWindow = 6 divides the 1-minute window into 6 ten-second segments.
//   Higher segment count = more precise sliding behaviour but slightly more memory.
//   6 segments is a good balance for a 1-minute window.
//
// QueueLimit = 0 — excess requests are rejected immediately with 429, not queued.
//   Queuing would hold connections open under attack, consuming server resources.
//
// RejectionStatusCode = 429 — ensures the standard "Too Many Requests" code is
//   returned rather than ASP.NET Core's default 503 Service Unavailable.
// -----------------------------------------------------------------------------
builder.Services.AddRateLimiter(options =>
{
    options.AddSlidingWindowLimiter("login", limiterOptions =>
    {
        limiterOptions.PermitLimit            = 5;
        limiterOptions.Window                 = TimeSpan.FromMinutes(1);
        limiterOptions.SegmentsPerWindow      = 6;
        limiterOptions.QueueProcessingOrder   = QueueProcessingOrder.OldestFirst;
        limiterOptions.QueueLimit             = 0;
    });
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
});

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.AddHealthChecks()
    .AddSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")!);

var app = builder.Build();

// =============================================================================
// Middleware pipeline — Level 1
//
// Order is critical. Each Use* call adds a component to the chain; every request
// flows through top-to-bottom, responses flow back bottom-to-top.
//
// Current pipeline order:
//   1. GlobalExceptionMiddleware   — outermost catch-all
//   2. Swagger (dev only)          — serves swagger.json and Swagger UI
//   3. HttpsRedirection            — redirects HTTP → HTTPS
//   4. RateLimiter                 — enforces [EnableRateLimiting] policies
//   5. Authentication              — validates JWT, populates HttpContext.User
//   6. Authorization               — enforces [Authorize] based on populated User
//   7. Controllers                 — routes requests to actions
//   8. Health checks               — lightweight /health endpoint
//
// Why RateLimiter before Authentication?
//   Rate limiting should reject abusive requests before spending CPU on token
//   validation. An attacker hammering the login endpoint should be turned away
//   at the rate limiter, not allowed to incur the cost of JWT parsing first.
//
// Why Authentication before Authorization?
//   Authorization checks what the caller is allowed to do — but it can only do
//   that after Authentication has established who the caller is (populated User).
//   Reversing the order would mean Authorization runs against an unauthenticated
//   User, causing all [Authorize] checks to fail even for valid tokens.
// =============================================================================

app.UseMiddleware<GlobalExceptionMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseRateLimiter();      // must be before Authentication so abuse is rejected early
app.UseAuthentication();   // validates JWT, populates HttpContext.User
app.UseAuthorization();    // enforces [Authorize] — must follow UseAuthentication

app.MapControllers();
app.MapHealthChecks("/health");

app.Run();
