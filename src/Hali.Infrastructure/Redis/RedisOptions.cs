namespace Hali.Infrastructure.Redis;

public class RedisOptions
{
    public const string Section = "Redis";

    public string Url { get; set; } = string.Empty;
}
