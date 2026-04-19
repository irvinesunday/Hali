using System.ComponentModel.DataAnnotations;

namespace Hali.Infrastructure.Redis;

public class RedisOptions
{
    public const string Section = "Redis";

    [Required]
    public string Url { get; set; } = string.Empty;
}
