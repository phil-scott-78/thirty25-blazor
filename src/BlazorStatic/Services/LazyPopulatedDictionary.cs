using System.Collections;
using System.Diagnostics.CodeAnalysis;

namespace BlazorStatic.Services;

/// <summary>
/// A read-only dictionary that populates its contents using a callback function.
/// Contents can be invalidated and repopulated on demand.
/// </summary>
/// <typeparam name="TKey">The type of keys in the dictionary.</typeparam>
/// <typeparam name="TValue">The type of values in the dictionary.</typeparam>
internal class LazyPopulatedDictionary<TKey, TValue> : IReadOnlyDictionary<TKey, TValue> where TKey : notnull
{
    private readonly Func<IEnumerable<KeyValuePair<TKey, TValue>>> _populateCallback;
    private readonly IDictionary<TKey, TValue> _backingDictionary;
    private readonly ReaderWriterLockSlim _lock = new();
    private bool _isInitialized;

    /// <summary>
    /// Initializes a new instance of the <see cref="LazyPopulatedDictionary{TKey, TValue}"/> class.
    /// </summary>
    /// <param name="populateCallback">A callback function that returns a dictionary to populate this instance.</param>
    public LazyPopulatedDictionary(Func<IEnumerable<KeyValuePair<TKey, TValue>>> populateCallback)
    {
        _populateCallback = populateCallback ?? throw new ArgumentNullException(nameof(populateCallback));
        _backingDictionary = new Dictionary<TKey, TValue>();
    }

    /// <summary>
    /// Gets the element with the specified key.
    /// </summary>
    /// <param name="key">The key of the element to get.</param>
    /// <returns>The element with the specified key.</returns>
    /// <exception cref="KeyNotFoundException">The key does not exist in the dictionary.</exception>
    public TValue this[TKey key]
    {
        get
        {
            EnsureInitialized();
            _lock.EnterReadLock();
            try
            {
                return _backingDictionary[key];
            }
            finally
            {
                _lock.ExitReadLock();
            }
        }
    }

    /// <summary>
    /// Gets the number of elements in the dictionary.
    /// </summary>
    public int Count
    {
        get
        {
            EnsureInitialized();
            _lock.EnterReadLock();
            try
            {
                return _backingDictionary.Count;
            }
            finally
            {
                _lock.ExitReadLock();
            }
        }
    }

    /// <summary>
    /// Gets a collection containing the keys in the dictionary.
    /// </summary>
    public IEnumerable<TKey> Keys
    {
        get
        {
            EnsureInitialized();
            _lock.EnterReadLock();
            try
            {
                // Return a new list to avoid enumeration issues if the dictionary changes
                return new List<TKey>(_backingDictionary.Keys);
            }
            finally
            {
                _lock.ExitReadLock();
            }
        }
    }

    /// <summary>
    /// Gets a collection containing the values in the dictionary.
    /// </summary>
    public IEnumerable<TValue> Values
    {
        get
        {
            EnsureInitialized();
            _lock.EnterReadLock();
            try
            {
                // Return a new list to avoid enumeration issues if the dictionary changes
                return new List<TValue>(_backingDictionary.Values);
            }
            finally
            {
                _lock.ExitReadLock();
            }
        }
    }

    /// <summary>
    /// Determines whether the dictionary contains an element with the specified key.
    /// </summary>
    /// <param name="key">The key to locate.</param>
    /// <returns>true if the dictionary contains an element with the key; otherwise, false.</returns>
    public bool ContainsKey(TKey key)
    {
        EnsureInitialized();
        _lock.EnterReadLock();
        try
        {
            return _backingDictionary.ContainsKey(key);
        }
        finally
        {
            _lock.ExitReadLock();
        }
    }

    /// <summary>
    /// Returns an enumerator that iterates through the dictionary.
    /// </summary>
    /// <returns>An enumerator that can be used to iterate through the dictionary.</returns>
    public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator()
    {
        EnsureInitialized();

        // Create a snapshot to avoid enumeration issues
        Dictionary<TKey, TValue> snapshot;

        _lock.EnterReadLock();
        try
        {
            snapshot = new Dictionary<TKey, TValue>(_backingDictionary);
        }
        finally
        {
            _lock.ExitReadLock();
        }

        return snapshot.GetEnumerator();
    }

    /// <summary>
    /// Returns an enumerator that iterates through the dictionary.
    /// </summary>
    /// <returns>An enumerator that can be used to iterate through the dictionary.</returns>
    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }

    /// <summary>
    /// Attempts to get the value associated with the specified key.
    /// </summary>
    /// <param name="key">The key of the value to get.</param>
    /// <param name="value">
    /// When this method returns, contains the value associated with the specified key, 
    /// if the key is found; otherwise, the default value for the type of the value parameter.
    /// </param>
    /// <returns>true if the dictionary contains an element with the specified key; otherwise, false.</returns>
    public bool TryGetValue(TKey key, [MaybeNullWhen(false)] out TValue value)
    {
        EnsureInitialized();
        _lock.EnterReadLock();
        try
        {
            return _backingDictionary.TryGetValue(key, out value);
        }
        finally
        {
            _lock.ExitReadLock();
        }
    }

    /// <summary>
    /// Invalidates the contents of the dictionary.
    /// The next access will repopulate the contents.
    /// </summary>
    public void Invalidate()
    {
        _lock.EnterWriteLock();
        try
        {
            _isInitialized = false;
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }

    private void EnsureInitialized()
    {
        if (_isInitialized) return;
        _lock.EnterUpgradeableReadLock();
        try
        {
            if (_isInitialized) return;

            _lock.EnterWriteLock();
            try
            {
                _backingDictionary.Clear();
                foreach (var kvp in _populateCallback())
                {
                    _backingDictionary.Add(kvp);
                }
                _isInitialized = true;
            }
            finally
            {
                _lock.ExitWriteLock();
            }
        }
        finally
        {
            _lock.ExitUpgradeableReadLock();
        }
    }
}