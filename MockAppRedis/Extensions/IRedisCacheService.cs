namespace MockAppRedis.Extensions;

public interface IRedisCacheService
{ 
    Task<T?> Get<T>(string key);
    Task Set<T>(string key, T value);   
}