using System;
using Hali.Application.Advisories;
using Hali.Application.Auth;
using Hali.Application.Clusters;
using Hali.Application.Notifications;
using Hali.Application.Participation;
using Hali.Application.Signals;
using Hali.Infrastructure.Advisories;
using Hali.Infrastructure.Auth;
using Hali.Infrastructure.Clusters;
using Hali.Infrastructure.Data;
using Hali.Infrastructure.Data.Advisories;
using Hali.Infrastructure.Data.Auth;
using Hali.Infrastructure.Data.Clusters;
using Hali.Infrastructure.Data.Notifications;
using Hali.Infrastructure.Data.Participation;
using Hali.Infrastructure.Data.Signals;
using Hali.Infrastructure.Notifications;
using Hali.Infrastructure.Participation;
using Hali.Infrastructure.Signals;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;

namespace Hali.Infrastructure.Extensions;

public static class ServiceCollectionExtensions
{
	public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration config)
	{
		var authDataSource = HaliNpgsqlDataSourceFactory.Build(config.GetConnectionString("Auth")!);
		services.AddDbContext<AuthDbContext>(opts => opts.UseNpgsql(authDataSource));

		var signalsDataSource = HaliNpgsqlDataSourceFactory.Build(config.GetConnectionString("Signals")!, useNetTopologySuite: true);
		services.AddDbContext<SignalsDbContext>(opts => opts.UseNpgsql(signalsDataSource, npgsql => npgsql.UseNetTopologySuite()));

		var clustersDataSource = HaliNpgsqlDataSourceFactory.Build(config.GetConnectionString("Clusters")!, useNetTopologySuite: true);
		services.AddDbContext<ClustersDbContext>(opts => opts.UseNpgsql(clustersDataSource, npgsql => npgsql.UseNetTopologySuite()));

		var participationDataSource = HaliNpgsqlDataSourceFactory.Build(config.GetConnectionString("Participation")!);
		services.AddDbContext<ParticipationDbContext>(opts => opts.UseNpgsql(participationDataSource));

		string redisUrl = config["Redis:Url"] ?? "localhost:6379";
		services.AddSingleton((Func<IServiceProvider, IConnectionMultiplexer>)((IServiceProvider _) => ConnectionMultiplexer.Connect(redisUrl)));
		services.AddSingleton((IServiceProvider sp) => sp.GetRequiredService<IConnectionMultiplexer>().GetDatabase());
		services.AddHttpClient<AfricasTalkingSmsProvider>();
		services.AddScoped<ISmsProvider, AfricasTalkingSmsProvider>();
		services.AddScoped<IAuthRepository, AuthRepository>();
		services.AddScoped<IInstitutionRepository, InstitutionRepository>();
		services.AddSingleton<IRateLimiter, RedisRateLimiter>();
		services.Configure<AfricasTalkingOptions>(config.GetSection("AfricasTalking"));
		services.AddScoped<ISignalRepository, SignalRepository>();
		services.AddScoped<ILocalityLookupRepository, LocalityLookupRepository>();
		services.AddHttpClient<AnthropicNlpExtractionService>();
		services.AddScoped<INlpExtractionService, AnthropicNlpExtractionService>();
		services.AddHttpClient<NominatimGeocodingService>();
		services.AddScoped<IGeocodingService, NominatimGeocodingService>();
		services.AddSingleton<IH3CellService, H3CellService>();
		services.AddScoped<IClusterRepository, ClusterRepository>();
		services.AddScoped<ICivisEvaluationService, CivisEvaluationService>();
		services.AddScoped<IClusteringService, ClusteringService>();
		services.AddScoped<IOutboxRelayService, OutboxRelayService>();
		services.Configure<CivisOptions>(config.GetSection("Civis"));
		services.AddScoped<IParticipationRepository, ParticipationRepository>();

		var advisoriesDataSource = HaliNpgsqlDataSourceFactory.Build(config.GetConnectionString("Advisories")!, useNetTopologySuite: true);
		services.AddDbContext<AdvisoriesDbContext>(opts => opts.UseNpgsql(advisoriesDataSource, npgsql => npgsql.UseNetTopologySuite()));
		services.AddScoped<IOfficialPostRepository, OfficialPostRepository>();

		// Notifications
		var notificationsDataSource = HaliNpgsqlDataSourceFactory.Build(
			config.GetConnectionString("Notifications") ?? config.GetConnectionString("Auth")!);
		services.AddDbContext<NotificationsDbContext>(opts => opts.UseNpgsql(notificationsDataSource));
		services.AddScoped<INotificationRepository, NotificationRepository>();
		services.AddScoped<IFollowRepository, FollowRepository>();
		services.AddHttpClient<ExpoPushNotificationService>();
		services.AddScoped<IPushNotificationService, ExpoPushNotificationService>();

		return services;
	}
}
