namespace Hali.Infrastructure.Auth;

public class AfricasTalkingOptions
{
	public const string Section = "AfricasTalking";

	public string ApiKey { get; set; } = string.Empty;

	public string Username { get; set; } = string.Empty;

	public string SenderId { get; set; } = string.Empty;
}
