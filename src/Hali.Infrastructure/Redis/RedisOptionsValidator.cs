using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Hali.Infrastructure.Redis;

/// <summary>
/// Validates <see cref="RedisOptions"/> at startup.
/// In Production, <c>Url</c> must be non-empty so the rate-limiting and
/// caching path cannot silently start against a default that may not exist.
/// Outside Production the default <c>localhost:6379</c> fallback is preserved
/// so local development works without explicit configuration.
/// </summary>
public sealed class RedisOptionsValidator : IValidateOptions<RedisOptions>
{
    private readonly IHostEnvironment _env;

    public RedisOptionsValidator(IHostEnvironment env)
    {
        _env = env;
    }

    public ValidateOptionsResult Validate(string? name, RedisOptions options)
    {
        if (!_env.IsProduction())
        {
            return ValidateOptionsResult.Success;
        }

        if (string.IsNullOrWhiteSpace(options.Url))
        {
            return ValidateOptionsResult.Fail(
                "Redis:Url is required in Production. " +
                "Set Redis:Url in configuration.");
        }

        return ValidateOptionsResult.Success;
    }
}
