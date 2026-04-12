using System;
using System.Threading;
using System.Threading.Tasks;

namespace Hali.Application.Auth;

public interface IRateLimiter
{
    Task<bool> IsAllowedAsync(string key, int maxRequests, TimeSpan window, CancellationToken ct = default(CancellationToken));
}
