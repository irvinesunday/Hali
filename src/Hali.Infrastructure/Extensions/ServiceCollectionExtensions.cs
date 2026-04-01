using Hali.Application.Auth;
using Hali.Application.Clusters;
using Hali.Application.Participation;
using Hali.Application.Signals;
using Hali.Domain.Enums;
using Hali.Infrastructure.Auth;
using Hali.Infrastructure.Clusters;
using Hali.Infrastructure.Data.Auth;
using Hali.Infrastructure.Data.Clusters;
using Hali.Infrastructure.Data.Participation;
using Hali.Infrastructure.Data.Signals;
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
        // PostgreSQL — use ConfigureDataSource to register enum type mappings.
        // HasPostgresEnum in OnModelCreating handles migration schema only;
        // runtime mapping is done here via ConfigureDataSource/MapEnum.
        services.AddDbContext<AuthDbContext>(opts =>
            opts.UseNpgsql(config.GetConnectionString("Auth"), npgsql =>
            {
                npgsql.MapEnum<AccountType>("account_type", nameTranslator: Snake);
                npgsql.MapEnum<AuthMethod>("auth_method", nameTranslator: Snake);
            }));

        services.AddDbContext<SignalsDbContext>(opts =>
            opts.UseNpgsql(config.GetConnectionString("Signals"), npgsql =>
            {
                npgsql.UseNetTopologySuite();
                npgsql.MapEnum<CivicCategory>("civic_category", nameTranslator: Snake);
                npgsql.MapEnum<LocationPrecisionType>("location_precision_type", nameTranslator: Snake);
            }));

        services.AddDbContext<ClustersDbContext>(opts =>
            opts.UseNpgsql(config.GetConnectionString("Clusters"), npgsql =>
            {
                npgsql.UseNetTopologySuite();
                npgsql.MapEnum<CivicCategory>("civic_category", nameTranslator: Snake);
                npgsql.MapEnum<SignalState>("signal_state", nameTranslator: Snake);
            }));

        services.AddDbContext<ParticipationDbContext>(opts =>
            opts.UseNpgsql(config.GetConnectionString("Participation"), npgsql =>
                npgsql.MapEnum<ParticipationType>("participation_type", nameTranslator: Snake)));

        // Redis
        var redisUrl = config["Redis:Url"] ?? "localhost:6379";
        services.AddSingleton<IConnectionMultiplexer>(_ => ConnectionMultiplexer.Connect(redisUrl));
        services.AddSingleton<IDatabase>(sp => sp.GetRequiredService<IConnectionMultiplexer>().GetDatabase());

        // Auth infrastructure services
        services.AddHttpClient<AfricasTalkingSmsProvider>();
        services.AddScoped<ISmsProvider, AfricasTalkingSmsProvider>();
        services.AddScoped<IAuthRepository, AuthRepository>();
        services.AddSingleton<IRateLimiter, RedisRateLimiter>();

        services.Configure<AfricasTalkingOptions>(config.GetSection(AfricasTalkingOptions.Section));

        // Signal infrastructure services
        services.AddScoped<ISignalRepository, SignalRepository>();
        services.AddHttpClient<AnthropicNlpExtractionService>();
        services.AddScoped<INlpExtractionService, AnthropicNlpExtractionService>();
        services.AddHttpClient<NominatimGeocodingService>();
        services.AddScoped<IGeocodingService, NominatimGeocodingService>();

        // Cluster infrastructure services
        services.AddSingleton<IH3CellService, H3CellService>();
        services.AddScoped<IClusterRepository, ClusterRepository>();
        services.AddScoped<ICivisEvaluationService, CivisEvaluationService>();
        services.AddScoped<IClusteringService, ClusteringService>();

        services.Configure<CivisOptions>(config.GetSection(CivisOptions.Section));

        // Participation infrastructure services
        services.AddScoped<IParticipationRepository, ParticipationRepository>();

        return services;
    }
}
