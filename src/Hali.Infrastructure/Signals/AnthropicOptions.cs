using System.ComponentModel.DataAnnotations;

namespace Hali.Infrastructure.Signals;

public class AnthropicOptions
{
    public const string Section = "Anthropic";

    [Required]
    public string ApiKey { get; set; } = string.Empty;

    public string Model { get; set; } = "claude-sonnet-4-6";
}
