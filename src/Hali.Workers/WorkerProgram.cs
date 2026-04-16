using Hali.Application.Clusters;
using Hali.Application.Advisories;
using Hali.Application.Notifications;
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

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddInfrastructure(builder.Configuration);

// PushNotificationsMetrics is a hard dependency of ExpoPushNotificationService,
// which is resolved inside SendPushNotificationsJob. Registered as a singleton
// so its underlying Meter is shared across every scoped send-service instance
// — matching the API composition root's pattern for the other observability
// meters. Safe to register here even though no OTel exporter is wired into
// the worker host: the Meter still exists in-process at zero cost.
builder.Services.AddSingleton<Hali.Application.Observability.PushNotificationsMetrics>();

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
