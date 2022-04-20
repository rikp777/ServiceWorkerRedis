using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.StackExchangeRedis;
using StackExchange.Redis;

namespace MockAppRedis.Extensions;

public class RedisCacheService : IRedisCacheService
{
   private readonly IDistributedCache _cache;
 
    public RedisCacheService(IDistributedCache cache)
    {
        _cache = cache;
    }

    public async Task<T?> Get<T>(string key)
    {
        var value = await _cache.GetStringAsync(key).ConfigureAwait(false);

        if (!string.IsNullOrEmpty(value))
        {
            return JsonSerializer.Deserialize<T>(value);
        }

        Console.WriteLine("does not exist");
        return default;
    }

    public async Task Set<T>(string key, T value)
    {
        var timeOut = new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(24),
            SlidingExpiration = TimeSpan.FromMinutes(60)
        };
 
        await _cache.SetStringAsync(key, JsonSerializer.Serialize(value), timeOut).ConfigureAwait(false);
    }
}