using Hali.Application.Clusters;
using Hali.Application.Advisories;
using Hali.Application.Notifications;
using Hali.Application.Observability;
using Hali.Application.Participation;
using Hali.Infrastructure.Clusters;
using Hali.Infrastructure.Data.Clusters;
using Hali.Infrastructure.Data.Advisories;
using Hali.Infrastructure.Data.Participation;
using Hali.Infrastructure.Advisories;
using Hali.Infrastructure.Participation;
using Hali.Infrastructure.Extensions;
using Hali.Workers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddInfrastructure(builder.Configuration);

// Metrics singletons — the Meter still exists in-process at zero cost when
// no OTel exporter is wired, matching the API composition root's pattern.
builder.Services.AddSingleton<PushNotificationsMetrics>();
builder.Services.AddSingleton<ClustersMetrics>();
builder.Services.AddSingleton<WorkerMetrics>();

// Worker correlation context — workers have no HTTP context, so
// ICorrelationContext resolves to a no-op implementation that returns
// Guid.Empty. Each worker applies the worker correlation rule: propagate
// an existing correlation id from the outbox event being processed, or
// generate a new root guid when there is none.
builder.Services.AddSingleton<ICorrelationContext, WorkerCorrelationContext>();

// OpenTelemetry for the worker process — enabled when OTEL_EXPORTER_OTLP_ENDPOINT is set.
string? otelEndpoint = builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]
    ?? System.Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT");

if (!string.IsNullOrWhiteSpace(otelEndpoint))
{
    builder.Services.AddOpenTelemetry()
        .ConfigureResource(r => r.AddService("hali-workers", serviceVersion: "1.0.0"))
        .WithMetrics(m => m
            .AddMeter(WorkerMetrics.MeterName)
            .AddMeter(ClustersMetrics.MeterName)
            .AddMeter(PushNotificationsMetrics.MeterName)
            .AddOtlpExporter(o => o.Endpoint = new System.Uri(otelEndpoint)));
}

// Notification services needed by workers
builder.Services.AddScoped<IFollowService, Hali.Application.Notifications.FollowService>();
builder.Services.AddScoped<INotificationQueueService, Hali.Application.Notifications.NotificationQueueService>();

builder.Services.AddHostedService<DecayActiveClustersJob>();
builder.Services.AddHostedService<ExpireOfficialPostsJob>();
builder.Services.AddHostedService<EvaluatePossibleRestorationJob>();
builder.Services.AddHostedService<SendPushNotificationsJob>();
builder.Services.AddHostedService<OutboxRelayJob>();

var host = builder.Build();
host.Run();
