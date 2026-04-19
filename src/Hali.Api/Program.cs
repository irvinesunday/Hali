using System;
using System.Runtime.InteropServices;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using Hali.Api.Errors;
using Hali.Api.Extensions;
using Hali.Api.Middleware;
using Hali.Api.Observability;
using Hali.Api.Startup;
using Microsoft.AspNetCore.DataProtection;
using Hali.Application.Auth;
using Hali.Application.Observability;
using Hali.Application.Errors;
using Hali.Application.Institutions;
using Hali.Application.Notifications;
using Hali.Application.Participation;
using Hali.Application.Signals;
using Hali.Application.Advisories;
using Hali.Infrastructure.Auth;
using Hali.Infrastructure.Data.DataProtection;
using Hali.Infrastructure.Extensions;
using Hali.Infrastructure.Redis;
using Hali.Infrastructure.Signals;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Sentry;

var builder = WebApplication.CreateBuilder(args);

// Fail-fast: production connection strings must all be present before any
// DB operation is attempted. Non-production is unaffected.
RequiredConnectionStrings.EnsureAllPresent(builder.Configuration, builder.Environment);

builder.Services.Configure<AuthOptions>(builder.Configuration.GetSection("Auth"));
builder.Services.Configure<OtpOptions>(builder.Configuration.GetSection("Otp"));
builder.Services.Configure<InstitutionAuthOptions>(builder.Configuration.GetSection("InstitutionAuth"));

// Options validation — fail-fast at startup for critical third-party secrets.
// In Production the validators enforce non-empty values; outside Production
// empty values are tolerated so local development works without real keys.
builder.Services
    .AddOptions<AfricasTalkingOptions>()
    .Bind(builder.Configuration.GetSection(AfricasTalkingOptions.Section))
    .ValidateDataAnnotations()
    .ValidateOnStart();
builder.Services.AddSingleton<IValidateOptions<AfricasTalkingOptions>, AfricasTalkingOptionsValidator>();

builder.Services
    .AddOptions<AnthropicOptions>()
    .Bind(builder.Configuration.GetSection(AnthropicOptions.Section))
    .ValidateDataAnnotations()
    .ValidateOnStart();
builder.Services.AddSingleton<IValidateOptions<AnthropicOptions>, AnthropicOptionsValidator>();

builder.Services
    .AddOptions<RedisOptions>()
    .Bind(builder.Configuration.GetSection(RedisOptions.Section))
    .ValidateDataAnnotations()
    .ValidateOnStart();
builder.Services.AddSingleton<IValidateOptions<RedisOptions>, RedisOptionsValidator>();

// Data Protection key ring (#243). TOTP secrets (#197) are encrypted
// with these keys — an unresolvable key ring breaks every institution
// user's second factor, so the ring must survive process restarts
// and be shared across nodes in a multi-node deployment.
//
// Key ring storage:   PostgreSQL (data_protection_keys) via
//                     HaliDataProtectionDbContext — constant across all
//                     environments.
// At-rest protection: X.509 certificate (PFX) loaded from the path
//                     configured in DataProtection:CertPath.
//                     Production MUST have a cert provisioned before
//                     institution users onboard; see
//                     docs/arch/SECURITY_POSTURE.md.
//
// Startup degradation:
//   * Cert configured + loads: keys protected — log filename only,
//     never the full path and never the password.
//   * Cert configured + missing/invalid + Production: throw. A
//     misconfigured production deployment must not silently start
//     unprotected.
//   * Cert configured + missing/invalid + non-Production: log ERROR
//     and continue so developers are not blocked by an ops issue.
//   * Cert unconfigured + Production: throw. DPAPI is not canonical
//     for Production even on Windows, and running unprotected under
//     Production is never acceptable per SECURITY_POSTURE.md §5.
//   * Cert unconfigured + Windows (non-Production): DPAPI
//     (dev-machine escape hatch only). Log WARNING.
//   * Cert unconfigured + other OS (non-Production): log WARNING,
//     keys unprotected.
//
// AddInfrastructure must run BEFORE AddDataProtection so the
// HaliDataProtectionDbContext is registered in DI when
// PersistKeysToDbContext<T>() resolves it.
builder.Services.AddInfrastructure(builder.Configuration);

var dataProtectionBuilder = builder.Services
    .AddDataProtection()
    .SetApplicationName("Hali.Api")
    .PersistKeysToDbContext<HaliDataProtectionDbContext>();

string? certPath = builder.Configuration["DataProtection:CertPath"];
string? certPassword = builder.Configuration["DataProtection:CertPassword"];

if (!string.IsNullOrWhiteSpace(certPath))
{
    if (System.IO.File.Exists(certPath))
    {
        try
        {
            // X509CertificateLoader is the .NET 9+ replacement for the
            // X509Certificate2 path-based constructor (which emits
            // SYSLIB0057 on .NET 10).
            X509Certificate2 cert = X509CertificateLoader.LoadPkcs12FromFile(
                certPath, certPassword);
            dataProtectionBuilder.ProtectKeysWithCertificate(cert);
            // Log the filename only — never the full path (side-channel
            // about deployment topology) and never the password.
            string certFileName = System.IO.Path.GetFileName(certPath);
            Console.Error.WriteLine(
                $"[data-protection] INFO: keys protected with certificate [{certFileName}]");
        }
        catch (Exception ex)
            when (ex is System.IO.IOException
                  || ex is UnauthorizedAccessException
                  || ex is System.Security.Cryptography.CryptographicException)
        {
            // Log the exception TYPE only — the message can leak path
            // fragments or crypto details.
            if (builder.Environment.IsProduction())
            {
                throw new InvalidOperationException(
                    "Data Protection certificate could not be loaded. " +
                    "Production cannot start unprotected.", ex);
            }
            Console.Error.WriteLine(
                "[data-protection] ERROR: certificate load failed — starting unprotected. " +
                $"Error type: {ex.GetType().Name}");
        }
    }
    else
    {
        // Cert configured but file absent. Production must fail loudly;
        // dev/staging log an ERROR and continue so a broken mount does
        // not block local work.
        if (builder.Environment.IsProduction())
        {
            throw new InvalidOperationException(
                "Data Protection certificate not found. Production cannot start unprotected.");
        }
        // Deliberately do NOT include the configured path — an attacker
        // who obtains this log should not learn where the system expects
        // its cert.
        Console.Error.WriteLine(
            "[data-protection] ERROR: certificate configured but file not found — starting unprotected");
    }
}
else if (builder.Environment.IsProduction())
{
    // Fail-fast: Production must never run unprotected. The posture
    // doc (docs/arch/SECURITY_POSTURE.md §5) names DataProtection:CertPath
    // a pre-onboarding requirement. DPAPI is explicitly NOT canonical for
    // production even on Windows — it is a Windows dev-machine escape hatch.
    throw new InvalidOperationException(
        "Data Protection certificate is required in Production. " +
        "Set DataProtection:CertPath to a provisioned PFX file.");
}
else if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
{
    dataProtectionBuilder.ProtectKeysWithDpapi(protectToLocalMachine: false);
    Console.Error.WriteLine(
        "[data-protection] WARNING: using DPAPI — Windows dev-machine only, NOT for staging/production");
}
else
{
    Console.Error.WriteLine(
        "[data-protection] WARNING: keys are NOT protected at rest — " +
        "set DataProtection:CertPath for staging/production");
}
// Correlation context — provides the live request correlation id to services
// that write outbox events so every event carries the request's trace id
// rather than an unrelated, freshly-minted GUID.
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICorrelationContext, CorrelationContext>();

builder.Services.AddScoped<IOtpService, OtpService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IInstitutionService, InstitutionService>();
builder.Services.AddScoped<ISignalIngestionService, SignalIngestionService>();
builder.Services.AddScoped<IParticipationService, ParticipationService>();
builder.Services.AddScoped<IOfficialPostsService, OfficialPostsService>();
// Institution operational dashboard read service (#195).
builder.Services.AddScoped<IInstitutionReadService, InstitutionReadService>();
// Institution acknowledge write path (#207 Phase 4 — explicit action,
// emits `institution.action.recorded`).
builder.Services.AddScoped<IInstitutionAcknowledgeService, InstitutionAcknowledgeService>();
// Phase 2 institution auth + session hardening (#197).
builder.Services.AddScoped<ITotpService, TotpService>();
builder.Services.AddScoped<IMagicLinkService, MagicLinkService>();
builder.Services.AddScoped<IInstitutionSessionService, InstitutionSessionService>();
// Audit hook for institution auth events (#251). No-op until the full audit
// infrastructure is merged — replace this binding with the real implementation.
builder.Services.AddScoped<Hali.Application.Auth.IAuthAuditService,
    Hali.Infrastructure.Auth.NoOpAuthAuditService>();
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

// ForwardedHeaders trust config — bind from ForwardedHeaders:KnownProxies and
// ForwardedHeaders:KnownNetworks in configuration so operators can add their
// load-balancer IPs/CIDRs without code changes. A startup log line reports the
// count of trusted entries (never the IPs themselves — topology side-channel).
// See src/Hali.Api/Extensions/ForwardedHeadersConfigurator.cs for the rules.
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    ForwardedHeadersConfigurator.ConfigureForwardedHeaders(
        options,
        builder.Configuration,
        builder.Environment,
        startupLogger: null);
});

var app = builder.Build();

app.UseForwardedHeaders();

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

// GET /health — liveness probe: returns 200 while the process is up.
// No dependency checks run here; a dependency outage does NOT indicate
// the process should be restarted. Load balancers that need dependency
// health should call /ready instead.
//
// Backwards compatibility: the response shape is preserved so existing
// LB/monitoring integrations do not require reconfiguration.
app.MapHealthChecks("/health", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    // Liveness = no dependency checks. The predicate excludes all named
    // checks (db, cache) so the endpoint always returns Healthy as long
    // as the process is running.
    Predicate = _ => false,
    ResponseWriter = async (ctx, _) =>
    {
        var result = new
        {
            status = "healthy",
            database = "unknown",
            redis = "unknown",
            version = "1.0.0",
            timestamp = DateTime.UtcNow.ToString("o")
        };
        ctx.Response.ContentType = "application/json";
        await ctx.Response.WriteAsync(JsonSerializer.Serialize(result));
    }
});

// GET /ready — readiness probe: returns 200 only when Postgres and Redis
// are reachable. Traffic should not be routed to a pod until this returns
// 200. A 503 response means the pod is alive but not yet ready to serve.
app.MapHealthChecks("/ready", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    Predicate = r => r.Tags.Contains("db") || r.Tags.Contains("cache"),
});

app.Run();
