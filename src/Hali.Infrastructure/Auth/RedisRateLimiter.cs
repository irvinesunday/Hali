using System;
using System.Threading;
using System.Threading.Tasks;
using Hali.Application.Auth;
using StackExchange.Redis;

namespace Hali.Infrastructure.Auth;

public class RedisRateLimiter : IRateLimiter
{
	private readonly IDatabase _db;

	public RedisRateLimiter(IConnectionMultiplexer redis)
	{
		_db = redis.GetDatabase();
	}

	public async Task<bool> IsAllowedAsync(string key, int maxRequests, TimeSpan window, CancellationToken ct = default(CancellationToken))
	{
		long count = await _db.StringIncrementAsync(key, 1L);
		if (count == 1)
		{
			await _db.KeyExpireAsync(key, window);
		}
		return count <= maxRequests;
	}
}
