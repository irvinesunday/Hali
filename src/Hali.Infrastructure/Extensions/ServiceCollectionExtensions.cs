using System;
using Hali.Application.Advisories;
using Hali.Application.Auth;
using Hali.Application.Clusters;
using Hali.Application.Notifications;
using Hali.Application.Participation;
using Hali.Application.Signals;
using Hali.Domain.Enums;
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
using Npgsql.NameTranslation;
using StackExchange.Redis;

namespace Hali.Infrastructure.Extensions;

public static class ServiceCollectionExtensions
{
	private static readonly NpgsqlSnakeCaseNameTranslator Snake = new();

	public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration config)
	{
		// Build all NpgsqlDataSources up front and hand them to a singleton
		// holder so the DI container owns + disposes the connection pools
		// on host shutdown (each DbContext resolves its data source from DI).
		var dataSources = new HaliDataSources(
			auth: HaliNpgsqlDataSourceFactory.Build(config.GetConnectionString("Auth")!),
			signals: HaliNpgsqlDataSourceFactory.Build(config.GetConnectionString("Signals")!, useNetTopologySuite: true),
			clusters: HaliNpgsqlDataSourceFactory.Build(config.GetConnectionString("Clusters")!, useNetTopologySuite: true),
			participation: HaliNpgsqlDataSourceFactory.Build(config.GetConnectionString("Participation")!),
			advisories: HaliNpgsqlDataSourceFactory.Build(config.GetConnectionString("Advisories")!, useNetTopologySuite: true),
			notifications: HaliNpgsqlDataSourceFactory.Build(
				config.GetConnectionString("Notifications") ?? config.GetConnectionString("Auth")!));
		services.AddSingleton(dataSources);

		// NOTE: Npgsql enum mapping must be declared on BOTH the data source
		// (so the ADO.NET layer recognizes the PG type) AND on the EF Core
		// options builder (so EF's model treats the column as an enum and
		// not as an int). Missing the EF side produces:
		//   42804: column "X" is of type X but expression is of type integer
		services.AddDbContext<AuthDbContext>((sp, opts) =>
			opts.UseNpgsql(sp.GetRequiredService<HaliDataSources>().Auth, npgsql =>
			{
				npgsql.MapEnum<AccountType>("account_type", null, Snake);
				npgsql.MapEnum<AuthMethod>("auth_method", null, Snake);
			}));

		services.AddDbContext<SignalsDbContext>((sp, opts) =>
			opts.UseNpgsql(sp.GetRequiredService<HaliDataSources>().Signals, npgsql =>
			{
				npgsql.UseNetTopologySuite();
				npgsql.MapEnum<CivicCategory>("civic_category", null, Snake);
				npgsql.MapEnum<LocationPrecisionType>("location_precision_type", null, Snake);
			}));

		services.AddDbContext<ClustersDbContext>((sp, opts) =>
			opts.UseNpgsql(sp.GetRequiredService<HaliDataSources>().Clusters, npgsql =>
			{
				npgsql.UseNetTopologySuite();
				npgsql.MapEnum<CivicCategory>("civic_category", null, Snake);
				npgsql.MapEnum<SignalState>("signal_state", null, Snake);
			}));

		services.AddDbContext<ParticipationDbContext>((sp, opts) =>
			opts.UseNpgsql(sp.GetRequiredService<HaliDataSources>().Participation, npgsql =>
			{
				npgsql.MapEnum<ParticipationType>("participation_type", null, Snake);
			}));

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

		services.AddDbContext<AdvisoriesDbContext>((sp, opts) =>
			opts.UseNpgsql(sp.GetRequiredService<HaliDataSources>().Advisories, npgsql =>
			{
				npgsql.UseNetTopologySuite();
				npgsql.MapEnum<CivicCategory>("civic_category", null, Snake);
				npgsql.MapEnum<OfficialPostType>("official_post_type", null, Snake);
			}));
		services.AddScoped<IOfficialPostRepository, OfficialPostRepository>();

		// Notifications
		services.AddDbContext<NotificationsDbContext>((sp, opts) =>
			opts.UseNpgsql(sp.GetRequiredService<HaliDataSources>().Notifications));
		services.AddScoped<INotificationRepository, NotificationRepository>();
		services.AddScoped<IFollowRepository, FollowRepository>();
		services.AddHttpClient<ExpoPushNotificationService>();
		services.AddScoped<IPushNotificationService, ExpoPushNotificationService>();

		return services;
	}
}
