namespace Hali.Infrastructure.Signals;

public class AnthropicOptions
{
    public const string Section = "Anthropic";

    public string ApiKey { get; set; } = string.Empty;

    public string Model { get; set; } = "claude-sonnet-4-6";
}
