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
using Npgsql.EntityFrameworkCore.PostgreSQL.Infrastructure;
using Npgsql.NameTranslation;
using StackExchange.Redis;

namespace Hali.Infrastructure.Extensions;

public static class ServiceCollectionExtensions
{
	private static readonly NpgsqlSnakeCaseNameTranslator Snake = new NpgsqlSnakeCaseNameTranslator();

	public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration config)
	{
		services.AddDbContext<AuthDbContext>(delegate(DbContextOptionsBuilder opts)
		{
			opts.UseNpgsql(config.GetConnectionString("Auth"), delegate(NpgsqlDbContextOptionsBuilder npgsql)
			{
				npgsql.MapEnum<AccountType>("account_type", null, Snake);
				npgsql.MapEnum<AuthMethod>("auth_method", null, Snake);
			});
		});
		services.AddDbContext<SignalsDbContext>(delegate(DbContextOptionsBuilder opts)
		{
			opts.UseNpgsql(config.GetConnectionString("Signals"), delegate(NpgsqlDbContextOptionsBuilder npgsql)
			{
				npgsql.UseNetTopologySuite();
				npgsql.MapEnum<CivicCategory>("civic_category", null, Snake);
				npgsql.MapEnum<LocationPrecisionType>("location_precision_type", null, Snake);
			});
		});
		services.AddDbContext<ClustersDbContext>(delegate(DbContextOptionsBuilder opts)
		{
			opts.UseNpgsql(config.GetConnectionString("Clusters"), delegate(NpgsqlDbContextOptionsBuilder npgsql)
			{
				npgsql.UseNetTopologySuite();
				npgsql.MapEnum<CivicCategory>("civic_category", null, Snake);
				npgsql.MapEnum<SignalState>("signal_state", null, Snake);
			});
		});
		services.AddDbContext<ParticipationDbContext>(delegate(DbContextOptionsBuilder opts)
		{
			opts.UseNpgsql(config.GetConnectionString("Participation"), delegate(NpgsqlDbContextOptionsBuilder npgsql)
			{
				npgsql.MapEnum<ParticipationType>("participation_type", null, Snake);
			});
		});
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
		services.AddDbContext<AdvisoriesDbContext>(delegate(DbContextOptionsBuilder opts)
		{
			opts.UseNpgsql(config.GetConnectionString("Advisories"), delegate(NpgsqlDbContextOptionsBuilder npgsql)
			{
				npgsql.UseNetTopologySuite();
				npgsql.MapEnum<CivicCategory>("civic_category", null, Snake);
				npgsql.MapEnum<OfficialPostType>("official_post_type", null, Snake);
			});
		});
		services.AddScoped<IOfficialPostRepository, OfficialPostRepository>();

		// Notifications
		services.AddDbContext<NotificationsDbContext>(delegate(DbContextOptionsBuilder opts)
		{
			opts.UseNpgsql(config.GetConnectionString("Notifications") ?? config.GetConnectionString("Auth"));
		});
		services.AddScoped<INotificationRepository, NotificationRepository>();
		services.AddScoped<IFollowRepository, FollowRepository>();
		services.AddHttpClient<ExpoPushNotificationService>();
		services.AddScoped<IPushNotificationService, ExpoPushNotificationService>();

		return services;
	}
}
