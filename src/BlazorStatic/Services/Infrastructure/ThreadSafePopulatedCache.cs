using System.Collections.Immutable;

namespace BlazorStatic.Services.Infrastructure;

/// <summary>
/// A thread-safe cache that lazily populates its contents using a callback function.
/// Contents can be invalidated and repopulated on demand, ensuring thread safety during access operations.
/// </summary>
/// <typeparam name="TKey">The type of keys in the cache.</typeparam>
/// <typeparam name="TValue">The type of values in the cache.</typeparam>
public class ThreadSafePopulatedCache<TKey, TValue> where TKey : notnull
{
    private readonly Func<Task<IEnumerable<KeyValuePair<TKey, TValue>>>> _populateCallback;

    // Single lock object for both initialization and dictionary access for simpler synchronization
    private readonly Lock _syncLock = new();

    // Backing dictionary - always accessed under lock
    private Dictionary<TKey, TValue> _backingDictionary;

    // Flag for initialization state - marked volatile for cross-thread visibility
    private volatile bool _isInitialized;

    // Task that represents the current initialization operation
    private Task _initializationTask;

    // Using a SemaphoreSlim to allow async waiting
    private readonly SemaphoreSlim _semaphore = new(1, 1);

    /// <summary>
    /// Initializes a new instance of the <see cref="ThreadSafePopulatedCache{TKey,TValue}"/> class.
    /// </summary>
    /// <param name="populateCallback">A callback function that returns key-value pairs to populate this cache.</param>
    public ThreadSafePopulatedCache(Func<Task<IEnumerable<KeyValuePair<TKey, TValue>>>> populateCallback)
    {
        _populateCallback = populateCallback ?? throw new ArgumentNullException(nameof(populateCallback));
        _backingDictionary = new Dictionary<TKey, TValue>();
        _initializationTask = Task.CompletedTask;
    }

    /// <summary>
    /// Gets the number of elements in the cache.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation. The task result contains the count of items in the cache.</returns>
    public async Task<int> GetCountAsync()
    {
        await EnsureInitializedAsync();

        lock (_syncLock)
        {
            return _backingDictionary.Count;
        }
    }

    /// <summary>
    /// Gets an immutable collection containing all values in the cache.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation. The task result contains an immutable list of all values.</returns>
    public async Task<ImmutableList<TValue>> GetValuesAsync()
    {
        await EnsureInitializedAsync();

        lock (_syncLock)
        {
            return _backingDictionary.Values.ToImmutableList();
        }
    }

    /// <summary>
    /// Attempts to get the value associated with the specified key.
    /// </summary>
    /// <param name="key">The key of the value to get.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains a tuple with a boolean indicating
    /// if the key was found, and the value (or default) associated with the key.</returns>
    public async Task<(bool Found, TValue? Value)> TryGetValueAsync(TKey key)
    {
        await EnsureInitializedAsync();

        lock (_syncLock)
        {
            var exists = _backingDictionary.TryGetValue(key, out var value);
            return exists ? (true, value) : (false, default);
        }
    }

    /// <summary>
    /// Invalidates the contents of the cache.
    /// The next access will trigger repopulation via the callback function.
    /// </summary>
    public void Invalidate()
    {
        lock (_syncLock)
        {
            Interlocked.Exchange(ref _isInitialized, false);
        }
    }

    /// <summary>
    /// Ensures the cache is initialized by populating it if necessary.
    /// </summary>
    /// <returns>A task that represents the asynchronous initialization operation.</returns>
    private async Task EnsureInitializedAsync()
    {
        // Fast path - check if already initialized
        if (_isInitialized)
        {
            return;
        }

        // Only one thread should initialize at a time
        await _semaphore.WaitAsync();
        try
        {
            // Double-check after acquiring the semaphore
            if (!_isInitialized)
            {
                try
                {
                    // Create a new task for this initialization
                    _initializationTask = InitializeInternalAsync();

                    // Wait for the initialization to complete
                    await _initializationTask;
                }
                catch
                {
                    // If initialization fails, allow future attempts
                    Interlocked.Exchange(ref _isInitialized, false);
                    throw;
                }
            }
            else
            {
                // Another thread already initialized, no need to do anything
            }
        }
        finally
        {
            _semaphore.Release();
        }
    }

    /// <summary>
    /// Internal method to perform the actual initialization.
    /// </summary>
    private async Task InitializeInternalAsync()
    {
        // Create a new dictionary to populate
        var newDictionary = new Dictionary<TKey, TValue>();

        // Get the data from the callback
        var results = await _populateCallback.Invoke();

        // Add all items to the dictionary
        foreach (var kvp in results)
        {
            newDictionary[kvp.Key] = kvp.Value; // Use indexer to handle duplicates
        }

        // Update the backing dictionary under lock to ensure thread safety
        lock (_syncLock)
        {
            _backingDictionary = newDictionary;
            Interlocked.Exchange(ref _isInitialized, true);
        }
    }
}