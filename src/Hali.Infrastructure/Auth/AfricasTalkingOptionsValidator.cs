using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Hali.Infrastructure.Auth;

/// <summary>
/// Validates <see cref="AfricasTalkingOptions"/> at startup.
/// In Production, both <c>ApiKey</c> and <c>Username</c> must be non-empty
/// so the SMS OTP path cannot silently start misconfigured.
/// Outside Production (Development / Staging / Testing) empty values are
/// tolerated so local development works without real credentials.
/// </summary>
public sealed class AfricasTalkingOptionsValidator : IValidateOptions<AfricasTalkingOptions>
{
    private readonly IHostEnvironment _env;

    public AfricasTalkingOptionsValidator(IHostEnvironment env)
    {
        _env = env;
    }

    public ValidateOptionsResult Validate(string? name, AfricasTalkingOptions options)
    {
        if (!_env.IsProduction())
        {
            return ValidateOptionsResult.Success;
        }

        if (string.IsNullOrWhiteSpace(options.ApiKey))
        {
            return ValidateOptionsResult.Fail(
                "AfricasTalking:ApiKey is required in Production. " +
                "Set AfricasTalking:ApiKey in configuration.");
        }

        if (string.IsNullOrWhiteSpace(options.Username))
        {
            return ValidateOptionsResult.Fail(
                "AfricasTalking:Username is required in Production. " +
                "Set AfricasTalking:Username in configuration.");
        }

        return ValidateOptionsResult.Success;
    }
}
