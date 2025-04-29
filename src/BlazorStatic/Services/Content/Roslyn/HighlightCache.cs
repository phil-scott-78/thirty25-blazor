using System.Collections.Concurrent;

namespace BlazorStatic.Services.Content.Roslyn;

internal class HighlightCache
{
    private readonly ConcurrentDictionary<int, string> _cache = new();

    public string GetOrAdd(string content, Func<string> factory)
    {
        // Calculate a hash for the content to use as the cache key
        var contentHash = content.GetHashCode();
        return _cache.GetOrAdd(contentHash, _ => factory());
    }
}