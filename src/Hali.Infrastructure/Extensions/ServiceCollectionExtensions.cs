using Hali.Application.Auth;
using Hali.Infrastructure.Auth;
using Hali.Infrastructure.Data.Auth;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;

namespace Hali.Infrastructure.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration config)
    {
        // PostgreSQL / Auth DB
        services.AddDbContext<AuthDbContext>(opts =>
            opts.UseNpgsql(config.GetConnectionString("Auth")));

        // Redis
        var redisUrl = config["Redis:Url"] ?? "localhost:6379";
        services.AddSingleton<IConnectionMultiplexer>(_ => ConnectionMultiplexer.Connect(redisUrl));

        // Auth infrastructure services
        services.AddHttpClient<AfricasTalkingSmsProvider>();
        services.AddScoped<ISmsProvider, AfricasTalkingSmsProvider>();
        services.AddScoped<IAuthRepository, AuthRepository>();
        services.AddSingleton<IRateLimiter, RedisRateLimiter>();

        services.Configure<AfricasTalkingOptions>(config.GetSection(AfricasTalkingOptions.Section));

        return services;
    }
}
