namespace Hali.Application.Auth;

public interface IRateLimiter
{
    /// <summary>Returns true if the request is allowed, false if rate limit exceeded.</summary>
    Task<bool> IsAllowedAsync(string key, int maxRequests, TimeSpan window, CancellationToken ct = default);
}
