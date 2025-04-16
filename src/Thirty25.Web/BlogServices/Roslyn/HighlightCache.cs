using System.Collections.Concurrent;

namespace Thirty25.Web.BlogServices.Roslyn;

internal class HighlightCache
{
    private readonly ConcurrentDictionary<int, string> _cache = new();

    public string GetOrAdd(string content, Func<string> factory)
    {
        // Calculate a hash for the content to use as cache key
        var contentHash = content.GetHashCode();
        return _cache.GetOrAdd(contentHash, _ => factory());
    }
}