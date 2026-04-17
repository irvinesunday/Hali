using System;
using System.Text;
using System.Text.Json;
using Hali.Api.Errors;
using Hali.Api.Middleware;
using Hali.Api.Observability;
using Microsoft.AspNetCore.DataProtection;
using Hali.Application.Auth;
using Hali.Application.Errors;
using Hali.Application.Institutions;
using Hali.Application.Notifications;
using Hali.Application.Participation;
using Hali.Application.Signals;
using Hali.Application.Advisories;
using Hali.Infrastructure.Extensions;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Sentry;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<AuthOptions>(builder.Configuration.GetSection("Auth"));
builder.Services.Configure<OtpOptions>(builder.Configuration.GetSection("Otp"));
builder.Services.Configure<InstitutionAuthOptions>(builder.Configuration.GetSection("InstitutionAuth"));

// Data Protection key persistence. TOTP secrets (#197) are encrypted
// with these keys — an unresolvable key ring breaks every institution
// user's second factor, so the ring must survive process restarts and
// be shared across nodes in a multi-node deployment. Rules:
//   * Production SHOULD set `DataProtection:KeysPath` to a pre-existing
//     writable path on a shared volume (or move to Redis / blob-backed
//     persistence). If unset, the app falls back to the content-root
//     subdirectory and the server logs a warning so operators can fix
//     the deployment without an outage.
//   * Development / Testing always fall back to the same content-root
//     subdirectory and the directory is created on demand.
// `Directory.CreateDirectory` is try-caught so a read-only filesystem
// surfaces a clear warning instead of an opaque startup crash — the
// DataProtection stack itself will then log its own detailed error
// when it can't write keys, giving operators two breadcrumbs.
// At-rest encryption of the persisted key ring (DPAPI / certificate /
// KMS) is a Production-only concern tracked as a follow-up — see
// issue #243.
string? configuredKeysPath = builder.Configuration["DataProtection:KeysPath"];
string dataProtectionKeysPath = configuredKeysPath
    ?? System.IO.Path.Combine(builder.Environment.ContentRootPath, "data-protection-keys");
try
{
    System.IO.Directory.CreateDirectory(dataProtectionKeysPath);
}
catch (Exception ex) when (ex is System.IO.IOException || ex is UnauthorizedAccessException)
{
    // Emit to stderr — the logging pipeline isn't up yet at this point
    // in Program.cs, and swallowing silently is worse than a visible
    // one-line startup note. DataProtection will fail loudly when it
    // tries to write a key, pointing operators at the real fix.
    Console.Error.WriteLine(
        $"[data-protection] WARNING: key path '{dataProtectionKeysPath}' is not writable. " +
        $"Configure DataProtection:KeysPath to a writable location. Cause: {ex.Message}");
}
if (builder.Environment.IsProduction() && configuredKeysPath is null)
{
    Console.Error.WriteLine(
        "[data-protection] WARNING: DataProtection:KeysPath is not configured. " +
        "In Production, TOTP secrets may be unrecoverable across restarts or nodes. " +
        "See issue #243 for at-rest protection follow-up.");
}
builder.Services
    .AddDataProtection()
    .PersistKeysToFileSystem(new System.IO.DirectoryInfo(dataProtectionKeysPath))
    .SetApplicationName("Hali.Api");
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddScoped<IOtpService, OtpService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IInstitutionService, InstitutionService>();
builder.Services.AddScoped<ISignalIngestionService, SignalIngestionService>();
builder.Services.AddScoped<IParticipationService, ParticipationService>();
builder.Services.AddScoped<IOfficialPostsService, OfficialPostsService>();
// Institution operational dashboard read service (#195).
builder.Services.AddScoped<IInstitutionReadService, InstitutionReadService>();
// Phase 2 institution auth + session hardening (#197).
builder.Services.AddScoped<ITotpService, TotpService>();
builder.Services.AddScoped<IMagicLinkService, MagicLinkService>();
builder.Services.AddScoped<IInstitutionSessionService, InstitutionSessionService>();
// Phase 2 institution-admin routes (#196).
builder.Services.AddScoped<Hali.Application.InstitutionAdmin.IInstitutionAdminService,
    Hali.Application.InstitutionAdmin.InstitutionAdminService>();
// Institution email sender — NoOp binding is deliberately restricted to
// non-Production environments. A production deployment must register
// its own IInstitutionEmailSender (real SES/SendGrid/etc) BEFORE this
// line, or the app will fail to resolve the scoped dependency on the
// first magic-link request.
if (!builder.Environment.IsProduction())
{
    builder.Services.AddScoped<Hali.Application.Auth.IInstitutionEmailSender,
        Hali.Infrastructure.Auth.NoOpInstitutionEmailSender>();
}
builder.Services.AddScoped<IFollowService, FollowService>();
builder.Services.AddScoped<INotificationQueueService, NotificationQueueService>();
builder.Services.AddSingleton<ExceptionToApiErrorMapper>();
// ApiMetrics owns the Hali.Api Meter + the api_exceptions_total counter. The
// instance is long-lived; IMeterFactory is registered by the hosting stack.
builder.Services.AddSingleton<ApiMetrics>();
// HomeMetrics owns the Hali.Home Meter + the home feed latency histogram and
// cache-hit/miss counters used by HomeController. Registered alongside
// ApiMetrics so both meters export through the same OTel pipeline.
builder.Services.AddSingleton<HomeMetrics>();
// SignalsMetrics owns the Hali.Signals Meter + the signal ingestion counters,
// NLP extraction latency histogram, and join-outcome counter emitted from
// SignalsController / AnthropicNlpExtractionService / ClusteringService /
// CivisEvaluationService. Same singleton lifetime as the other meters.
builder.Services.AddSingleton<Hali.Application.Observability.SignalsMetrics>();
// ClustersMetrics owns the Hali.Clusters Meter + the participation actions
// counter and cluster lifecycle transitions counter emitted from
// ClustersController / ParticipationService / CivisEvaluationService.
builder.Services.AddSingleton<Hali.Application.Observability.ClustersMetrics>();
// PushNotificationsMetrics owns the Hali.Notifications Meter + the push-send
// attempt counter, push-send latency histogram, and push-token registration
// counter emitted from ExpoPushNotificationService / DevicesController.
builder.Services.AddSingleton<Hali.Application.Observability.PushNotificationsMetrics>();

// Typed feature flag registry + evaluator. Stateless, in-process, singleton.
// See docs/arch/FEATURE_FLIGHTING_MODEL.md for the policy and
// src/Hali.Application/FeatureFlags/FeatureFlags.cs for the canonical catalog.
builder.Services.AddSingleton<Hali.Application.FeatureFlags.IFeatureFlagService,
    Hali.Application.FeatureFlags.FeatureFlagService>();

// Context factory wires JWT claims + IHostEnvironment into
// FeatureFlagEvaluationContext for the /v1/feature-flags endpoint.
builder.Services.AddSingleton<Hali.Api.FeatureFlags.IFeatureFlagContextFactory,
    Hali.Api.FeatureFlags.FeatureFlagContextFactory>();

string jwtSecret = builder.Configuration["Auth:JwtSecret"]
    ?? throw new InvalidOperationException("Auth:JwtSecret is required");
string jwtIssuer = builder.Configuration["Auth:JwtIssuer"] ?? "hali";
string jwtAudience = builder.Configuration["Auth:JwtAudience"] ?? "hali-platform";

builder.Services.AddAuthentication("Bearer").AddJwtBearer(opts =>
{
    opts.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = jwtIssuer,
        ValidAudience = jwtAudience,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret))
    };

    // Replace the framework's default empty-bodied 401 with the canonical
    // ApiErrorResponse envelope. Without this, every [Authorize]-protected
    // endpoint short-circuits before ExceptionHandlingMiddleware can write
    // a body, breaking the wire contract every other error path honours.
    //
    // Code is "auth.unauthenticated" — distinct from the application-layer
    // "auth.unauthorized" thrown by controllers when a token is valid but
    // an expected claim is missing. We deliberately do NOT leak whether
    // the token was missing, malformed, or expired (security side-channel).
    opts.Events = new JwtBearerEvents
    {
        OnChallenge = async context =>
        {
            // Suppress the framework's default challenge so we own the response.
            context.HandleResponse();

            if (context.Response.HasStarted)
            {
                return;
            }

            // Preserve RFC 7235 challenge advertisement.
            if (!context.Response.Headers.ContainsKey("WWW-Authenticate"))
            {
                context.Response.Headers.Append("WWW-Authenticate", "Bearer");
            }

            // CorrelationIdMiddleware runs before authentication, so
            // Items["CorrelationId"] is populated and already sanitized to
            // a server-only GUID. TraceIdentifier is the framework fallback.
            var traceId = context.HttpContext.Items["CorrelationId"] as string
                ?? context.HttpContext.TraceIdentifier
                ?? string.Empty;

            var envelope = new ApiErrorResponse
            {
                Error = new ApiErrorBody
                {
                    Code = ErrorCodes.AuthUnauthenticated,
                    Message = "Authentication required.",
                    TraceId = traceId
                }
            };

            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync(
                JsonSerializer.Serialize(envelope, ApiErrorJsonOptions.Default));
        },

        // Mirror of OnChallenge for the 403 path: when an authenticated caller
        // fails a role-gated [Authorize(Roles = ...)] check, the authorization
        // stage short-circuits with a 403 that bypasses
        // ExceptionHandlingMiddleware, producing a bare empty body. This hook
        // emits the canonical ApiErrorResponse envelope instead so role-gated
        // endpoints match the wire contract used everywhere else.
        //
        // Code is "auth.role_insufficient" — distinct from:
        //   - "auth.unauthenticated" (OnChallenge above — no/invalid token)
        //   - "auth.unauthorized" (application-layer, claim missing in logic)
        //   - "auth.refresh_token_invalid" (refresh-endpoint-specific)
        // Message is deliberately opaque: we do NOT leak the required role,
        // policy, or claim name, because that information exposes the shape
        // of the authorization graph to unauthorized callers.
        OnForbidden = async context =>
        {
            if (context.Response.HasStarted)
            {
                return;
            }

            var traceId = context.HttpContext.Items["CorrelationId"] as string
                ?? context.HttpContext.TraceIdentifier
                ?? string.Empty;

            var envelope = new ApiErrorResponse
            {
                Error = new ApiErrorBody
                {
                    Code = ErrorCodes.AuthRoleInsufficient,
                    Message = "Access to this resource is not permitted.",
                    TraceId = traceId
                }
            };

            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync(
                JsonSerializer.Serialize(envelope, ApiErrorJsonOptions.Default));
        }
    };
});

builder.Services.AddAuthorization();
builder.Services.AddControllers()
    .AddJsonOptions(Hali.Api.Serialization.ApiJsonConfiguration.Configure);
builder.Services.AddOpenApi();

// OpenTelemetry — enabled when OTEL_EXPORTER_OTLP_ENDPOINT is configured
string? otelEndpoint = builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]
    ?? Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT");

if (!string.IsNullOrWhiteSpace(otelEndpoint))
{
    builder.Services.AddOpenTelemetry()
        .ConfigureResource(r => r.AddService("hali-api", serviceVersion: "1.0.0"))
        .WithTracing(t => t
            .AddAspNetCoreInstrumentation(o => o.RecordException = true)
            .AddHttpClientInstrumentation()
            .AddEntityFrameworkCoreInstrumentation()
            .AddOtlpExporter(o => o.Endpoint = new Uri(otelEndpoint)))
        .WithMetrics(m => m
            .AddAspNetCoreInstrumentation()
            .AddMeter(ApiMetrics.MeterName)
            .AddMeter(HomeMetrics.MeterName)
            .AddMeter(Hali.Application.Observability.SignalsMetrics.MeterName)
            .AddMeter(Hali.Application.Observability.ClustersMetrics.MeterName)
            .AddMeter(Hali.Application.Observability.PushNotificationsMetrics.MeterName)
            .AddOtlpExporter(o => o.Endpoint = new Uri(otelEndpoint)));
}

// Sentry — enabled when SENTRY_DSN is configured
string? sentryDsn = builder.Configuration["SENTRY_DSN"]
    ?? Environment.GetEnvironmentVariable("SENTRY_DSN");

if (!string.IsNullOrWhiteSpace(sentryDsn))
{
    SentrySdk.Init(o =>
    {
        o.Dsn = sentryDsn;
        o.SendDefaultPii = false;
        o.TracesSampleRate = 0.1;
    });
}

// Health checks
builder.Services.AddHealthChecks()
    .AddNpgSql(
        builder.Configuration.GetConnectionString("Auth") ?? string.Empty,
        name: "database",
        failureStatus: HealthStatus.Unhealthy,
        tags: new[] { "db" })
    .AddRedis(
        builder.Configuration["Redis:Url"] ?? "localhost:6379",
        name: "redis",
        failureStatus: HealthStatus.Unhealthy,
        tags: new[] { "cache" });

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseMiddleware<CorrelationIdMiddleware>();
app.UseMiddleware<ExceptionHandlingMiddleware>();
app.UseAuthentication();
// Session cookie middleware runs AFTER authentication so Bearer-JWT flows
// short-circuit it. The institution middleware only applies to requests
// without a JWT header that target institution routes.
app.UseMiddleware<InstitutionSessionMiddleware>();
app.UseAuthorization();
// CSRF enforcement must see the AuthorizationPolicy has already passed
// for the current request — but runs before the controller so write
// verbs are rejected before they touch domain code. We position it
// after UseAuthorization to keep that ordering.
app.UseMiddleware<InstitutionCsrfMiddleware>();
app.MapControllers();

// GET /health
app.MapGet("/health", async (Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckService healthService) =>
{
    var report = await healthService.CheckHealthAsync();
    var dbStatus = report.Entries.TryGetValue("database", out var db) ? db.Status.ToString().ToLowerInvariant() : "unknown";
    var redisStatus = report.Entries.TryGetValue("redis", out var redis) ? redis.Status.ToString().ToLowerInvariant() : "unknown";

    var result = new
    {
        status = report.Status == HealthStatus.Healthy ? "healthy" : "unhealthy",
        database = dbStatus == "healthy" ? "connected" : dbStatus,
        redis = redisStatus == "healthy" ? "connected" : redisStatus,
        version = "1.0.0",
        timestamp = DateTime.UtcNow.ToString("o")
    };

    return report.Status == HealthStatus.Healthy
        ? Results.Ok(result)
        : Results.Json(result, statusCode: 503);
});

app.Run();
