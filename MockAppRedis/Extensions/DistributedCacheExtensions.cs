using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;

namespace MockAppRedis.Extensions;

public class RedisOptions
{
    public string? BaseUrl { get; set; }
    public string? Password { get; set; }
}

public static class DistributedCacheExtensions
{
    
    public static IServiceCollection AddRedis(
        this IServiceCollection services, 
        IConfiguration configuration
        )
    {
        // Get redis section from config
        var section = configuration.GetRequiredSection("ConnectionStrings:Redis");
        var options = new RedisOptions();
        
        section.Bind(options);
        services.Configure<RedisOptions>(section);

        var connectionString = $"{options.BaseUrl}";
        
        // Fail app when Redis connection fails 
        services.AddSingleton<IRedisCacheService, RedisCacheService>();
        services.AddSingleton<RedisPubSub>();
        services.AddStackExchangeRedisCache(options =>
        {
            options.Configuration = connectionString;
            options.InstanceName = "master";
        });
        return services;
    }

    public static async Task Publish<T>()
    {
        const string RedisConnectionString = "localhost:1998";
        ConnectionMultiplexer connection = ConnectionMultiplexer.Connect(RedisConnectionString);
        const string Channel = "test-channel";

        var pubsub = connection.GetSubscriber();
        pubsub.Publish(Channel, "Balblalkdjaslkdjf", CommandFlags.FireAndForget);
        
    }

    public static async Task Subscribe<T>()
    {
        const string RedisConnectionString = "localhost:1998";
        ConnectionMultiplexer connection = ConnectionMultiplexer.Connect(RedisConnectionString);
        const string Channel = "test-channel";

        var pubsub = connection.GetSubscriber();
        pubsub.Subscribe(Channel, (channel, message) =>
        {
            Console.WriteLine("hooiv " + message);
        });
        
    }
    public static async Task SetRecordAsync<T>(
        this IDistributedCache cache,
        string recordId,
        T data,
        TimeSpan? absoluteExpireTime = null,
        TimeSpan? unusedExpireTime = null
    )
    {
        var options = new DistributedCacheEntryOptions();
        // Auto expire in absoluteExpireTime or default 356 days 
        options.AbsoluteExpirationRelativeToNow = absoluteExpireTime ?? TimeSpan.FromDays(2 * 356);
        // If data not used in timespan auto expire
        options.SlidingExpiration = unusedExpireTime ?? TimeSpan.FromDays(356);

        var jsonData = JsonSerializer.Serialize(data);
        await cache.SetStringAsync(recordId, jsonData, options);
    }

    public static async Task<T?> GetRecordAsync<T>(
        this IDistributedCache cache,
        string recordId
    )
    {
        try
        {
            var jsonData = await cache.GetStringAsync(recordId);
            if (jsonData is null)
            {
                // return null of type T 
                return default(T);
            }
            return JsonSerializer.Deserialize<T>(jsonData);
        }
        catch (Exception e)
        {
            throw new Exception(e.Message);
        }

        return default;
    }
}