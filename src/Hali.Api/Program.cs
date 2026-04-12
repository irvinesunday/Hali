using System;
using System.Text;
using Hali.Api.Errors;
using Hali.Api.Middleware;
using Hali.Application.Auth;
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
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddScoped<IOtpService, OtpService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IInstitutionService, InstitutionService>();
builder.Services.AddScoped<ISignalIngestionService, SignalIngestionService>();
builder.Services.AddScoped<IParticipationService, ParticipationService>();
builder.Services.AddScoped<IOfficialPostsService, OfficialPostsService>();
builder.Services.AddScoped<IFollowService, FollowService>();
builder.Services.AddScoped<INotificationQueueService, NotificationQueueService>();
builder.Services.AddSingleton<ExceptionToApiErrorMapper>();

string jwtSecret = builder.Configuration["Auth:JwtSecret"]
    ?? throw new InvalidOperationException("Auth:JwtSecret is required");
string jwtIssuer = builder.Configuration["Auth:JwtIssuer"] ?? "hali";
string jwtAudience = builder.Configuration["Auth:JwtAudience"] ?? "hali";

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
});

builder.Services.AddAuthorization();
builder.Services.AddControllers()
    .AddJsonOptions(opts =>
    {
        opts.JsonSerializerOptions.Converters.Add(
            new System.Text.Json.Serialization.JsonStringEnumConverter(
                System.Text.Json.JsonNamingPolicy.SnakeCaseLower));
        opts.JsonSerializerOptions.PropertyNamingPolicy =
            System.Text.Json.JsonNamingPolicy.CamelCase;
    });
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
app.UseAuthorization();
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
