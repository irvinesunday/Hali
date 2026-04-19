using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Hali.Infrastructure.Signals;

/// <summary>
/// Validates <see cref="AnthropicOptions"/> at startup.
/// In Production, <c>ApiKey</c> must be non-empty so the NLP extraction
/// path cannot silently start misconfigured.
/// Outside Production empty values are tolerated so local development works
/// without real credentials.
/// </summary>
public sealed class AnthropicOptionsValidator : IValidateOptions<AnthropicOptions>
{
    private readonly IHostEnvironment _env;

    public AnthropicOptionsValidator(IHostEnvironment env)
    {
        _env = env;
    }

    public ValidateOptionsResult Validate(string? name, AnthropicOptions options)
    {
        if (!_env.IsProduction())
        {
            return ValidateOptionsResult.Success;
        }

        if (string.IsNullOrWhiteSpace(options.ApiKey))
        {
            return ValidateOptionsResult.Fail(
                "Anthropic:ApiKey is required in Production. " +
                "Set Anthropic:ApiKey in configuration.");
        }

        return ValidateOptionsResult.Success;
    }
}
