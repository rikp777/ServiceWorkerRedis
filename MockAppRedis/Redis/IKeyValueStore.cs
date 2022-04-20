namespace MockAppRedis.Redis;

/// <summary>
/// Interface to a key-value store
/// </summary>
public interface IKeyValueStore
{
    #region TryGet

    /// <summary>
    /// Get a value from the key value store, null if not found
    /// </summary>
    /// <typeparam name="T">The type of the value returned from the key value store</typeparam>
    /// <param name="key">The key to retrieve from the key value store</param>
    /// <param name="value">The value returned from the value store as type <typeparamref name="T"/> or a default value if not found</param>
    /// <returns>True if the value is found, false if not.</returns>
    /// <remarks>Serialization is done by the implementation of the key value store</remarks>
    T TryGet<T>(string key);

    /// <summary>
    /// Get a value from the key value store, null if not found async
    /// </summary>
    /// <typeparam name="T">The type of the value returned from the key value store</typeparam>
    /// <param name="key">The key to retrieve from the key value store</param>
    /// <param name="value">The value returned from the value store as type <typeparamref name="T"/> or a default value if not found</param>
    /// <param name="cancellationToken"><see cref="CancellationToken"/> to cancel the process</param>
    /// <returns><see cref="Task"/> containing True if the value is found, false if not.</returns>
    /// <remarks><see cref="Task"/> containing Serialization is done by the implementation of the key value store</remarks>
    Task<T?> TryGetAsync<T>(string key, CancellationToken cancellationToken);

    #endregion

    #region GetUntilValueAsync

    /// <summary>
    /// Get the value from the key value store.
    /// If the value is not found a watch will be created to watch for a value
    /// </summary>
    /// <typeparam name="T">The type of the value returned from the key value store</typeparam>
    /// <param name="key">The key to retrieve from the key value store</param>
    /// <param name="waitTimeout">The maximum wait timeout to wait for a value</param>
    /// <returns>The value of the key value store.</returns>
    T GetUntilValue<T>(string key, TimeSpan waitTimeout);
    
    /// <summary>
    /// Get the value from the key value store async
    /// If the value is not found a watch will be created to watch for a value
    /// </summary>
    /// <typeparam name="T">The type of the value returned from the key value store</typeparam>
    /// <param name="key">The key to retrieve from the key value store</param>
    /// <param name="cancellationToken">If the <see cref="CancellationToken"/> is set the watch/wait will be cancelled as soon as possible</param>
    /// <param name="waitTimeout">The maximum wait timeout to wait for a value</param>
    /// <returns><see cref="Task"/> containing The value of the key value store.</returns>
    Task<T> GetUntilValueAsync<T>(string key, CancellationToken cancellationToken, TimeSpan waitTimeout);

    #endregion
    
    #region TrySet
    
    /// <summary>
    /// Set the value for the giving key in the key value store
    /// </summary>
    /// <typeparam name="T">The type of the value stored in the key value store</typeparam>
    /// <param name="key">The key to store the value in the value store</param>
    /// <param name="value">The value to store in the value store</param>
    /// <param name="expiry">The point in time after which the key should expire</param>
    /// <param name="overWrite">Value indicating whether the value can be overwritten in the store. Default true.</param>
    /// <returns>True if the value is set, false if not.</returns>
    bool TrySet<T>(string key, T value, TimeSpan? expiry = null, bool overWrite = true);

    /// <summary>
    /// Set the value for the giving key in the key value store async
    /// </summary>
    /// <typeparam name="T">The type of the value stored in the key value store</typeparam>
    /// <param name="key">The key to store the value in the value store</param>
    /// <param name="value">The value to store in the value store</param>
    /// <param name="cancellationToken"><see cref="CancellationToken"/> to cancel the process</param>
    /// <param name="expiry">The point in time after which the key should expire</param>
    /// <param name="overWrite">Value indicating whether the value can be overwritten in the store. Default true.</param>
    /// <returns><see cref="Task"/> containing True if the value is set, false if not.</returns>
    Task<bool> TrySetAsync<T>(string key, T value, CancellationToken cancellationToken, TimeSpan? expiry = null, bool overWrite = true);
    
    #endregion
    
    #region AddToList

    /// <summary>
    /// Registers value <paramref name="value"/> to redis under key <paramref name="key"/>
    /// </summary>
    /// <typeparam name="T">The type of value stored to the list in the vault</typeparam>
    /// <param name="key">The key used to add the item to the list</param>
    /// <param name="value">The value to add to the list</param>
    /// <returns>True if registered successfully, false if not</returns>
    bool AddToList<T>(string key, T value);

    /// <summary>
    /// Registers value <paramref name="value"/> to redis under key <paramref name="key"/> async
    /// </summary>
    /// <typeparam name="T">The type of value stored to the list in the vault</typeparam>
    /// <param name="key">The key used to add the item to the list</param>
    /// <param name="value">The value to add to the list</param>
    /// <param name="cancellationToken"><see cref="CancellationToken"/> to cancel the process</param>
    /// <returns><see cref="Task"/> containing True if registered successfully, false if not</returns>
    Task<bool> AddToListAsync<T>(string key, T value, CancellationToken cancellationToken);
    
    #endregion
    
    #region RemoveFromList

    /// <summary>
    /// Removes value <paramref name="value"/> to redis under key <paramref name="key"/>
    /// </summary>
    /// <typeparam name="T">The type of value stored to the list in the vault</typeparam>
    /// <param name="key">The key used to remove the item from the list</param>
    /// <param name="value">The value to remove from the list</param>
    /// <returns>True if removed successfully, false if not</returns>
    bool RemoveFromList<T>(string key, T value);

    /// <summary>
    /// Removes value <paramref name="value"/> to redis under key <paramref name="key"/> async
    /// </summary>
    /// <typeparam name="T">The type of value stored to the list in the vault</typeparam>
    /// <param name="key">The key used to remove the item from the list</param>
    /// <param name="value">The value to remove from the list</param>
    /// <param name="cancellationToken"><see cref="CancellationToken"/> to cancel the process</param>
    /// <returns><see cref="Task"/> containing True if removed successfully, false if not</returns>
    Task<bool> RemoveFromListAsync<T>(string key, T value, CancellationToken cancellationToken);

    #endregion

    #region GetListItems

    /// <summary>
    /// Retrieves the list under key <paramref name="key"/>
    /// </summary>
    /// <typeparam name="T">Type of element</typeparam>
    /// <param name="key">The key to retrieve the list from the vault</param>
    /// <returns>List of values of type T</returns>
    List<T> GetListItems<T>(string key);

    /// <summary>
    /// Retrieves the list under key <paramref name="key"/> async
    /// </summary>
    /// <typeparam name="T">Type of element</typeparam>
    /// <param name="key">The key to retrieve the list from the vault</param>
    /// <param name="cancellationToken"><see cref="CancellationToken"/> to cancel the process</param>
    /// <returns><see cref="Task"/> containing List of values of type T</returns>
    Task<List<T>> GetListItemsAsync<T>(string key, CancellationToken cancellationToken);
    #endregion
}