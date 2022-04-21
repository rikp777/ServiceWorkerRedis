namespace MockAppRedis.Redis;

public interface IPubSub
{
    #region TryPublish
    /// <summary>
    /// Try publish a <paramref name="message"/> to the specified <paramref name="topic"/>
    /// </summary>
    /// <typeparam name="T">The type of the value of the message</typeparam>
    /// <param name="topic">The topic to publish the message to</param>
    /// <param name="message">The message value to publish</param>
    /// <returns>True if publishing succeeded, false if not.</returns>
    bool TryPublish<T>(string topic, T message);

    /// <summary>
    /// Try publish a <paramref name="message"/> to the specified <paramref name="topic"/> async
    /// </summary>
    /// <typeparam name="T">The type of the value of the message</typeparam>
    /// <param name="topic">The topic to publish the message to</param>
    /// <param name="message">The message value to publish</param>
    /// <param name="cancellationToken"><see cref="CancellationToken"/> to cancel the process</param>
    /// <returns><see cref="Task"/> containing True if publishing succeeded, false if not.</returns>
    Task<bool> TryPublishAsync<T>(string topic, T message, CancellationToken cancellationToken);
    #endregion
    
    #region SubscribeSequentially
    /// <summary>
    /// Subscribe to a <paramref name="topic"/> and execute a function when it gets a message on said channel in a sequential fashion
    /// </summary>
    /// <param name="topic">The topic to subscribe on.</param>
    /// <param name="executionMethod"><see cref="Action"/> to execute when a message gets sent to the channel.</param>
    /// <returns>True if subscribing succeeded, false if not.</returns>
    bool SubscribeSequentially<T>(string topic, Action<T> executionMethod);
    
    /// <summary>
    /// Subscribe to a <paramref name="topic"/> and execute a function when it gets a message on said channel in a sequential fashion async
    /// </summary>
    /// <param name="topic">The topic to subscribe on.</param>
    /// <param name="executionMethod"><see cref="Action"/> to execute when a message gets sent to the channel.</param>
    /// <param name="cancellationToken"><see cref="CancellationToken"/> to cancel the process</param>
    /// <returns><see cref="Task"/> containing True if subscribing succeeded, false if not.</returns>
    Task<bool> SubscribeSequentiallyAsync<T>(string topic, Action<T> executionMethod, CancellationToken cancellationToken);
    #endregion

    #region SubscribeConcurrently
    /// <summary>
    /// Subscribe to a <paramref name="topic"/> and execute a function when it gets a message on said channel in a concurrent fashion
    /// </summary>
    /// <param name="topic">The topic to subscribe on.</param>
    /// <param name="executionMethod"><see cref="Action"/> to execute when a message gets sent to the channel.</param>
    /// <returns>True if subscribing succeeded, false if not.</returns>
    bool SubscribeConcurrently<T>(string topic, Action<T> executionMethod);
    /// <summary>
    /// Subscribe to a <paramref name="topic"/> and execute a function when it gets a message on said channel in a concurrent fashion async
    /// </summary>
    /// <param name="topic">The topic to subscribe on.</param>
    /// <param name="executionMethod"><see cref="Action"/> to execute when a message gets sent to the channel.</param>
    /// <param name="cancellationToken">to cancel the process</param>
    /// <returns><see cref="Task"/> containing True if subscribing succeeded, false if not.</returns>
    Task SubscribeConcurrentlyAsync<T>(string topic, Action<T> executionMethod, CancellationToken cancellationToken);
    #endregion

    #region UnsubScribe

    /// <summary>
    /// Unsubscribe from a <paramref name="topic"/>
    /// </summary>
    /// <param name="topic">The topic that the client subscribed on.</param>
    /// <returns>True is unsubscribing succeeded, false if not.</returns>
    bool UnsubScribe(string topic);
    
    /// <summary>
    /// Unsubscribe from a <paramref name="topic"/> async
    /// </summary>
    /// <param name="topic">The topic that the client subscribed on.</param>
    /// <param name="cancellationToken">to cancel the process</param>
    /// <returns><see cref="Task"/> containing True is unsubscribing succeeded, false if not. In <see cref="Task"/></returns>
    Task<bool> UnsubScribeAsync(string topic, CancellationToken cancellationToken);

    #endregion
    
}