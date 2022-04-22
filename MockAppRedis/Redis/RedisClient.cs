using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Serilog;
using StackExchange.Redis;


namespace MockAppRedis.Redis;

/// <summary>
/// A client that connect to Redis
/// </summary>
/// <inheritdoc cref="IKeyValueStore" />
/// <inheritdoc cref="IPubSub" />
public sealed class RedisClient : IKeyValueStore, IPubSub
{
    #region Params

    /// <summary>
    /// Static dictionary that provides the same <see cref="ConnectionMultiplexer"/> for every <seealso cref="ConfigurationOptions"/>
    /// The <see cref="ConnectionMultiplexer"/> should be persistent. It is designed to be shared and reused between callers.
    /// </summary>
    private static readonly ConcurrentDictionary<ConfigurationOptions, ConnectionMultiplexer> ConnectionMultiplexerManager = new();
    
    /// <summary>
    /// The redis connection multiplexer for the current client
    /// </summary>
    private readonly ConnectionMultiplexer _redisConnection;
    
    /// <summary>
    /// The specific logger for this client
    /// </summary>
    private readonly ILogger _log;
    
    /// <summary>
    /// To detect redundant calls
    /// </summary>
    private bool _disposedValue;
    
    /// <summary>
    /// Retry delay for the <see cref="GetUntilValueAsync{T}"/> method
    /// </summary>
    private const int RetryDelay = 1000;
    
    /// <summary>
    /// For subscription on keyspace
    /// </summary>
    private readonly string? _channelPrefix;

    #endregion

    #region Constructors
    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="endPoints">Endpoint addresses for the Redis service.</param>
    /// <param name="channelPrefix">Channel prefix to separate the channels</param>

    public RedisClient(List<string> endPoints, string? channelPrefix, ILogger logger)
    {
        if (endPoints == null || endPoints.Count == 0)
            throw new ArgumentNullException(nameof(endPoints));

        _channelPrefix = channelPrefix;
        var redisConfiguration = new ConfigurationOptions()
        {
            ChannelPrefix = _channelPrefix,
            AbortOnConnectFail = false,
            ConnectRetry = 10,
            ReconnectRetryPolicy = new ExponentialRetry(5000),
            KeepAlive = 60
        };

        foreach (var endpoint in endPoints)
        {
            redisConfiguration.EndPoints.Add(endpoint);
        }

        _redisConnection = ConnectionMultiplexerManager
            .GetOrAdd(
                redisConfiguration, 
                (conf) => ConnectionMultiplexer.Connect(conf)
            );

        _log = logger;
    }
    #endregion
    
    // Functions 
    public IConnectionMultiplexer getRedisConnection()
    {
        return _redisConnection;
    }

    #region IKeyValueStore impls
    
    
    
    
    
    

    #region TryGet
    /// <inheritdoc />
    public bool TryGet<T>(string key, out T value)
    {
        // Validation
        ValidateKey(key);

        // Set default 
        value = default;
        key = GetPrefixKey(key);
        
        // Get data
        RedisValue rawString;
        try
        {
            var db = _redisConnection.GetDatabase();

            //Check if the key exists in the store
            if (!db.KeyExists(key))
                return false;
            
            //Get value from the store
            rawString = db.StringGet(key);
        }
        catch (Exception ex)
        {
            _log.Error($"Error while getting key '{key}': {ex.Message}");
            return false;
        }

        return TryDeserialize(key, rawString, out value);
    }
    /// <inheritdoc />
    public async Task<(bool success, T? value)> TryGetAsync<T>(string key, CancellationToken cancellationToken)
    {
        //TODO in future implement use cancellationToken  
        // Validation
        ValidateKey(key);
        
        // Build prefix 
        key = GetPrefixKey(key);
        
        // Get data 
        RedisValue rawString;
        try
        {
            var db = _redisConnection.GetDatabase();

            //Check if the key exists in the store
            if (! await db.KeyExistsAsync(key))
                return (false, default);
            
            //Get value from the store
            rawString = await db.StringGetAsync(key);
        }
        catch (Exception ex)
        {
            _log.Error($"Error while getting key '{key}': {ex.Message}");
            return (false, default);
        }

        var success = TryDeserialize(key, rawString, out T? value);
        
        return (success, value);
    }

    #endregion

    #region GetUntilValue
    /// <inheritdoc />
    public T GetUntilValue<T>(string key, TimeSpan waitTimeout)
    {
        key = GetPrefixKey(key);
        
        try
        {
            var db = _redisConnection.GetDatabase();
            //Check if the key was maybe already created
            var keyFound = db.KeyExists(key); ;

            var timeOut = new Stopwatch();
            timeOut.Start();
            while (!keyFound && (timeOut.Elapsed <= waitTimeout))
            {
                keyFound = db.KeyExists(key);
            }
            timeOut.Stop();

            if (timeOut.Elapsed >= waitTimeout)
                throw new TimeoutException($"Timeout on Redis Store when trying to resolve key: {key}");

            var stringValue = db.StringGet(key);

            _log.Information("GetUntilValueAsync on '{KeyName}': took {TimeOutValue}", key, timeOut.Elapsed);

            return JsonSerializer.Deserialize<T>(stringValue);
        }
        catch (TimeoutException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _log.Error(ex, "Error while getting value with subscription: {ErrorMessage}", ex.Message);
            return default;
        }
    }
    /// <inheritdoc />
    public async Task<T?> GetUntilValueAsync<T>(string key, CancellationToken cancellationToken, TimeSpan waitTimeout)
    {
        key = GetPrefixKey(key);

        try
        {
            //Throw if we were already cancelled
            cancellationToken.ThrowIfCancellationRequested();

            var db = _redisConnection.GetDatabase();
            //Check if the key was maybe already created
            var keyFound = await db.KeyExistsAsync(key); ;

            var timeOut = new Stopwatch();
            timeOut.Start();
            while (!keyFound && (timeOut.Elapsed <= waitTimeout))
            {
                //Possible cleanup when cancellation token is set
                if (cancellationToken.IsCancellationRequested)
                {
                    timeOut.Stop();
                    cancellationToken.ThrowIfCancellationRequested();
                }
                keyFound = await db.KeyExistsAsync(key);
                await Task.Delay(RetryDelay, cancellationToken);
            }
            timeOut.Stop();

            if (timeOut.Elapsed >= waitTimeout)
                throw new TimeoutException($"Timeout on Redis Store when trying to resolve key: {key}");

            var stringValue = await db.StringGetAsync(key);

            _log.Information("GetUntilValueAsync on '{KeyName}': took {TimeOutValue}", key, timeOut.Elapsed);

            var success = TryDeserialize(key, stringValue, out T? deserializedValue);
            return !success ? default : deserializedValue;
        }
        catch (TimeoutException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _log.Error(ex, "Error while getting value with subscription: {ErrorMessage}", ex.Message);
            return default;
        }
    }

    #endregion

    #region TrySet
    /// <inheritdoc />
    public bool TrySet<T>(string key, T value, TimeSpan? expiry = null, bool overWrite = true)
    {
        // Validation 
        ValidateKey(key);
        ValidateValue(value);

        // Build key string 
        key = GetPrefixKey(key);

        // Serializer  
        var success = TrySerialize(value, out var setValue);
        if (!success)
            return false;

        try
        {
            var db = _redisConnection.GetDatabase();
            return db.StringSet(key, setValue, expiry);
        }
        catch (Exception e)
        {
            // ReSharper disable once TemplateIsNotCompileTimeConstantProblem
            _log.Error(e, $"Error whilesetting key {key} : {e.Message}");
            return false;
        }
    }
    /// <inheritdoc />
    public async Task<bool> TrySetAsync<T>(string key, T value, CancellationToken cancellationToken, TimeSpan? expiry = null,
        bool overWrite = true)
    {
        // Validation 
        ValidateKey(key);
        ValidateValue(value);

        // Build key string 
        key = GetPrefixKey(key);

        // Serializer  
        var success = TrySerialize(value, out var setValue);
        if (!success)
            return false;

        try
        {
            var db = _redisConnection.GetDatabase();
            return await db.StringSetAsync(key, setValue, expiry);
        }
        catch (Exception e)
        {
            // ReSharper disable once TemplateIsNotCompileTimeConstantProblem
            _log.Error(e, $"Error whilesetting key {key} : {e.Message}");
            return false;
        }
    }


    #endregion

    #region AddToList
    /// <inheritdoc />
    public bool AddToList<T>(string key, T value)
    {
        // Validation 
        ValidateKey(key);
        ValidateValue(value);

        // Serialize
        var success = TrySerialize(value, out var valueToAdd);
        if (!success)
            return false;

        try
        {
            var db = _redisConnection.GetDatabase();
            db.ListRightPush(key, valueToAdd);
            return true;
        }
        catch(Exception ex)
        {
            _log.Error(
                $"An error occurred while adding value {value} to the list with key {key} : {ex.Message}");
            return false;
        }
    }
    /// <inheritdoc />
    public async Task<bool> AddToListAsync<T>(string key, T value, CancellationToken cancellationToken)
    {
        // Validation 
        ValidateKey(key);
        ValidateValue(value);

        // Serialize
        var success = TrySerialize(value, out var valueToAdd);
        if (!success)
            return false;

        try
        {
            var db = _redisConnection.GetDatabase();
            await db.ListRightPushAsync(key, valueToAdd);
            return true;
        }
        catch(Exception ex)
        {
            _log.Error(
                $"An error occurred while adding value {value} to the list with key {key} : {ex.Message}");
            return false;
        }
    }

    #endregion

    #region RemoveFromList
    /// <inheritdoc />
    public bool RemoveFromList<T>(string key, T value)
    {
        // Validation 
        ValidateKey(key);
        ValidateValue(value);
        
        // Serialize
        var success = TrySerialize(value, out var valueToRemove);
        if (!success)
            return false;
        
        try
        {
            var db = _redisConnection.GetDatabase();
            db.ListRemove(key, valueToRemove);
            return true;
        }
        catch(Exception ex)
        {
            _log.Error(
                $"An error occurred while removing value {value} to the list with key {key} : {ex.Message}");
            return false;
        }
    }
    /// <inheritdoc />
    public async Task<bool> RemoveFromListAsync<T>(string key, T value, CancellationToken cancellationToken)
    {
        // Validation 
        ValidateKey(key);
        ValidateValue(value);
        
        // Serialize
        var success = TrySerialize(value, out var valueToRemove);
        if (!success)
            return false;
        
        try
        {
            var db = _redisConnection.GetDatabase();
            await db.ListRemoveAsync(key, valueToRemove);
            return true;
        }
        catch(Exception ex)
        {
            _log.Error(
                $"An error occurred while removing value {value} to the list with key {key} : {ex.Message}");
            return false;
        }
    }

    #endregion

    #region GetListItems
    /// <inheritdoc />
    public List<T> GetListItems<T>(string key)
    {
        // Validation 
        ValidateKey(key);

        try
        {
            var db = _redisConnection.GetDatabase();
            var redisValues = db.ListRange(key);
            var values = redisValues.Select(value => JsonSerializer.Deserialize<T>(value)).ToList();
            return values;
        }
        catch (Exception ex)
        {
            _log.Error($"An error occurred while trying to retrieve the list from Redis with key {key}: {ex.Message}");
            throw;
        }
    }
    /// <inheritdoc />
    public async Task<List<T>> GetListItemsAsync<T>(string key, CancellationToken cancellationToken)
    {
        // Validation 
        ValidateKey(key);

        try
        {
            var db = _redisConnection.GetDatabase();
            var redisValues = await db.ListRangeAsync(key);
            var values = redisValues.Select(value => JsonSerializer.Deserialize<T>(value)).ToList();
            return values;
        }
        catch (Exception ex)
        {
            _log.Error($"An error occurred while trying to retrieve the list from Redis with key {key}: {ex.Message}");
            throw;
        }
    }

    #endregion




    #endregion

   

    #region IPubSub

    
    
    
    
    #region TryPublish
    /// <inheritdoc />
    public bool TryPublish<T>(string topic, T message)
    {
        // Build topic string 
        topic = GetPrefixKey(topic);

        // Validation 
        ValidateTopic(topic);
        ValidateMessage(message);

        //Serialization 
        var success = TrySerialize(topic, message, out var publishValue);
        if (!success)
            return false;

        //Publish 
        try
        {
            //_log.LogInformation($"Publish on {topic} : {publishValue}");
            var sub = _redisConnection.GetSubscriber();
            _ = sub.Publish(topic, publishValue);
            return true;
        }
        catch (Exception ex)
        {
            _log.Error($"Error while publishing message to topic '{topic}': {ex.Message}");
            return false;
        }
    }
    /// <inheritdoc />
    public async Task<bool> TryPublishAsync<T>(string topic, T message, CancellationToken cancellationToken)
    {
        // Build topic string 
        topic = GetPrefixKey(topic);

        // Validation 
        ValidateTopic(topic);
        ValidateMessage(message);

        //Serialization 
        var success = TrySerialize(topic, message, out var publishValue );
        if (success)
            return false;
        
        //Publish 
        try
        {
            var sub = _redisConnection.GetSubscriber();
            _ = await sub.PublishAsync(topic, publishValue);
            return true;
        }
        catch (Exception ex)
        {
            _log.Error($"Error while publishing message to topic '{topic}': {ex.Message}");
            return false;
        }
    }
    
    #endregion

    #region SubscribeSequentially
    /// <inheritdoc />
    public bool SubscribeSequentially<T>(string topic, Action<T> executionMethod)
    {
        return SubscribeInternal(topic, executionMethod, true);
    }
    /// <inheritdoc />
    public Task<bool> SubscribeSequentiallyAsync<T>(string topic, Action<T> executionMethod, CancellationToken cancellationToken)
    {
        return SubscribeInternalAsync(topic, executionMethod, true);
    }

    #endregion

    #region SubscribeConcurrently
    /// <inheritdoc />
    public bool SubscribeConcurrently<T>(string topic, Action<T> executionMethod)
    {
        return SubscribeInternal(topic, executionMethod, true);
    }

    public Task SubscribeConcurrentlyAsync<T>(string topic, Action<T> executionMethod, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }


    public async Task SubscribeConcurrentlyAsync<T>(string topic, Action action, CancellationToken cancellationToken) where T : IDistributedWorkItem 
    {
        topic = GetPrefixKey(topic);
        
        var sub = _redisConnection.GetSubscriber();
        
        var counter = 0;
        var distributedQueue = new DistributedQueue<IDistributedWorkItem?>(
            "Vertex",
            (item, token) =>
            {
                item.ExpensiveWork();
                Interlocked.Increment(ref counter);
            },
            _log, 
            10,
            cancellationToken
        );
        
        
        Log.Information("Starting sub..");
        var channelQueue = sub.Subscribe(topic);
        _log.Information($"Subscribed to topic: {topic}");
        channelQueue.OnMessage((m) =>
        {
            //_log.LogInformation($"New message on topic: {topic} worker {counter}");
            var deserializedWorkItem = JsonSerializer.Deserialize<T>(m.Message);
            distributedQueue.TryEnqueueWorkItem(deserializedWorkItem);
        });
        await Task.Run(() => distributedQueue.ScheduleWorkItems(), cancellationToken);
    }

    #endregion
    
    #region UnsubScribe 
    /// <inheritdoc />
    public bool UnsubScribe(string topic)
    {
        
        topic = GetPrefixKey(topic);

        if (string.IsNullOrEmpty(topic))
            throw new ArgumentNullException(nameof(topic));

        try
        {
            var sub = _redisConnection.GetSubscriber();
            sub.Unsubscribe(topic);
            return true;
        }
        catch (Exception e)
        {
            _log.Error($"Error while unsubscribing from topic {topic} : {e.Message}");
            return false;
        }
    }
    /// <inheritdoc />
    public async Task<bool> UnsubScribeAsync(string topic, CancellationToken cancellationToken)
    {
        topic = GetPrefixKey(topic);

        if (string.IsNullOrEmpty(topic))
            throw new ArgumentNullException(nameof(topic));

        try
        {
            var sub = _redisConnection.GetSubscriber();
            await sub.UnsubscribeAsync(topic);
            return true;
        }
        catch (Exception e)
        {
            _log.Error($"Error while unsubscribing from topic {topic} : {e.Message}");
            return false;
        }
    }
    #endregion
        
        
    
        
    #endregion
    
    
    
    // Helpers 
    /// <summary>
    /// Build prefix key based on key input
    /// </summary>
    /// <param name="key">Key value</param>
    /// <returns>string</returns>
    private string GetPrefixKey(string key)
    {
        return string.IsNullOrWhiteSpace(_channelPrefix) ? key : $"{_channelPrefix}:{key}";
    }

    private void HandleChannelMessage<T>(RedisValue redisValue, string topic, Action<T> executionMethod)
    {
        T messageValue = default;

        try
        {
            messageValue = JsonSerializer.Deserialize<T>(redisValue) ?? throw new InvalidOperationException(nameof(redisValue));
        }
        catch (Exception ex)
        {
            _log.Error($"Error while deserializing json string to object on topic {topic} : {ex.Message}");
        }

        try
        {
            executionMethod(messageValue);
        }
        catch (Exception ex)
        {
            _log.Error($"Error while executing deserialized json {topic} : {ex.Message}");
        }
    }

    private bool SubscribeInternal<T>(string topic, Action<T> executionMethod, bool sequentially)
    {
        // Validation
        ValidateTopic(topic);
        ValidateExecutionMethod(executionMethod);

        // Build topic string 
        topic = GetPrefixKey(topic);

        try
        {
            var sub = _redisConnection.GetSubscriber();
            if (sequentially)
            {
                var channelQueue = sub.Subscribe(topic);
                channelQueue.OnMessage((m) => HandleChannelMessage(m.Message, topic, executionMethod));
            }
            else
            {
                sub.Subscribe(topic, (c,v) => HandleChannelMessage(v, topic, executionMethod));
            }

            return true;
        }
        catch(Exception e)
        {
            _log.Error($"Error during subscribe function: {e.Message}");
            return false;
        }
    }
    private async Task<bool> SubscribeInternalAsync<T>(string topic, Action<T> executionMethod, bool sequentially)
    {
        // Validation
        ValidateTopic(topic);
        ValidateExecutionMethod(executionMethod);

        // Build topic string 
        topic = GetPrefixKey(topic);

        try
        {
            var sub = _redisConnection.GetSubscriber();
            if (sequentially)
            {
                var channelQueue = await sub.SubscribeAsync(topic);
                channelQueue.OnMessage((m) => HandleChannelMessage(m.Message, topic, executionMethod));
            }
            else
            {
                await sub.SubscribeAsync(topic, (c,v) => HandleChannelMessage(v, topic, executionMethod));
            }

            return true;
        }
        catch(Exception e)
        {
            _log.Error($"Error during subscribe function: {e.Message}");
            return false;
        }
    }

    #region Validation
    
    private static void ValidateValue<T>(T value)
    {
        if (Equals(value, default(T)) && typeof(T) != typeof(bool))
            throw new ArgumentNullException(nameof(value));
    }
    private static void ValidateKey(string key)
    {
        if (string.IsNullOrEmpty(key))
            throw new ArgumentNullException(nameof(key));
    }
    private static void ValidateTopic(string topic)
    {
        if (string.IsNullOrEmpty(topic))
            throw new ArgumentNullException(nameof(topic));
    }

    private static void ValidateMessage<T>(T message)
    {
        if (Equals(message, default(T)))
            throw new ArgumentNullException(nameof(message));
    }

    private static void ValidateExecutionMethod<T>(Action<T> executionMethod)
    {
        if (executionMethod == null)
            throw new ArgumentNullException(nameof(executionMethod));
    }
    #endregion
    
    
    #region Serialize & Deserialize 
    /// <summary>
    /// Try to serialize message  
    /// </summary>
    /// <param name="message">Input to serialize</param>
    /// <param name="serializedValue">Output of serialization</param>
    /// <param name="topic">Topic where serialization for is preformed</param>
    /// <typeparam name="T">Type of the value</typeparam>
    /// <returns>True if the value is set, false if not.</returns>
    private bool TrySerialize<T>(string topic, T message, out RedisValue serializedValue)
    {
        serializedValue = default;
        try
        {
            serializedValue = JsonSerializer.Serialize(message);
            return true;
        }
        catch (Exception ex)
        {
            // ReSharper disable once TemplateIsNotCompileTimeConstantProblem
            _log.Error($"Error while serializing object to json string from {topic} : {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Try to serialize value 
    /// </summary>
    /// <param name="value">Input to serialize</param>
    /// <param name="serializedValue">Output of serialization</param>
    /// <typeparam name="T">Type of the value</typeparam>
    /// <returns>True if the value is set, false if not.</returns>
    private bool TrySerialize<T>(T value, out RedisValue serializedValue)
    {
        serializedValue = default;
        try
        {
            serializedValue = JsonSerializer.Serialize(value);
            return true;
        }
        catch (Exception e)
        {
            // ReSharper disable once TemplateIsNotCompileTimeConstantProblem
            _log.Error($"Error while serializing object to json string for value {value}: {e.Message}");
            return false;
        }
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="key">Key where deserialization for is preformed</param>
    /// <param name="value">Input to deserialize</param>
    /// <param name="deserializedValue">Output of deserialization</param>
    /// <typeparam name="T">Type of the value</typeparam>
    /// <returns>True if the value is set, false if not.</returns>
    private bool TryDeserialize<T>(string key, string value, out T? deserializedValue)
    {
        deserializedValue = default;
        try
        {
            deserializedValue = JsonSerializer.Deserialize<T>(value);
            return true;
        }
        catch (Exception ex)
        {
            // ReSharper disable once TemplateIsNotCompileTimeConstantProblem
            _log.Error($"Error while deserializing json string to object for key '{key}': {ex.Message}");
            return false;
        }
    }
    #endregion
}