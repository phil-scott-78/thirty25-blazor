using System.Collections.Immutable;

namespace BlazorStatic.Services;

/// <summary>
/// A thread-safe cache that lazily populates its contents using a callback function.
/// Contents can be invalidated and repopulated on demand, ensuring thread safety during access operations.
/// </summary>
/// <typeparam name="TKey">The type of keys in the cache.</typeparam>
/// <typeparam name="TValue">The type of values in the cache.</typeparam>
internal class ThreadSafePopulatedCache<TKey, TValue> where TKey : notnull
{
    private readonly Func<Task<IEnumerable<KeyValuePair<TKey, TValue>>>> _populateCallback;
    private readonly IDictionary<TKey, TValue> _backingDictionary;
    private readonly SemaphoreSlim _semaphore = new(1, 1);
    private bool _isInitialized;

    /// <summary>
    /// Initializes a new instance of the <see cref="ThreadSafePopulatedCache{TKey,TValue}"/> class.
    /// </summary>
    /// <param name="populateCallback">A callback function that returns key-value pairs to populate this cache.</param>
    public ThreadSafePopulatedCache(Func<Task<IEnumerable<KeyValuePair<TKey, TValue>>>> populateCallback)
    {
        _populateCallback = populateCallback ?? throw new ArgumentNullException(nameof(populateCallback));
        _backingDictionary = new Dictionary<TKey, TValue>();
    }

    /// <summary>
    /// Gets the number of elements in the cache.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation. The task result contains the count of items in the cache.</returns>
    public async Task<int> GetCountAsync()
    {
        await EnsureInitializedAsync();

        // No need for a lock when reading after initialization
        return _backingDictionary.Count;
    }

    /// <summary>
    /// Gets an immutable collection containing all values in the cache.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation. The task result contains an immutable list of all values.</returns>
    public async Task<ImmutableList<TValue>> GetValuesAsync()
    {
        await EnsureInitializedAsync();

        // Create an immutable copy to ensure thread safety
        return _backingDictionary.Values.ToImmutableList();
    }

    /// <summary>
    /// Attempts to get the value associated with the specified key.
    /// </summary>
    /// <param name="key">The key of the value to get.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains a tuple with a boolean indicating
    /// if the key was found, and the value (or default) associated with the key.</returns>
    public async Task<(bool, TValue?)> TryGetValueAsync(TKey key)
    {
        await EnsureInitializedAsync();

        var exists = _backingDictionary.TryGetValue(key, out var value);
        return exists ? (true, value) : (false, default);
    }

    /// <summary>
    /// Invalidates the contents of the cache.
    /// The next access will trigger repopulation via the callback function.
    /// </summary>
    public void Invalidate()
    {
        _semaphore.Wait();
        try
        {
            _isInitialized = false;
        }
        finally
        {
            _semaphore.Release();
        }
    }

    /// <summary>
    /// Ensures the cache is initialized by populating it if necessary.
    /// Uses a double-check locking pattern to minimize lock contention.
    /// </summary>
    /// <returns>A task that represents the asynchronous initialization operation.</returns>
    private async Task EnsureInitializedAsync()
    {
        // Quick check without acquiring the semaphore
        if (_isInitialized) return;

        await _semaphore.WaitAsync();
        try
        {
            // Double-check after acquiring the semaphore
            if (!_isInitialized)
            {
                _backingDictionary.Clear();
                var results = await _populateCallback.Invoke();
                foreach (var kvp in results)
                {
                    _backingDictionary.Add(kvp);
                }

                _isInitialized = true;
            }
        }
        finally
        {
            _semaphore.Release();
        }
    }
}