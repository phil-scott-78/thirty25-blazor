

// [assembly: MetadataUpdateHandler(typeof(CssClassCollector))]

namespace Thirty25.Web.BlogServices.Styling;

internal class CssClassCollector
{
    // at one point we were using MetaDataUpdateHandler, but it was causing issues with the
    // timing. Sometimes the browser refresh would happen before the call for this ClearCache would occur
    // which would result in the classes being added to the collection then immediately removed.
    // 
    // For now we'll just keep adding to the Classes hashset and not worry about clearing it.
    // It'll cause some css classes to be added that are not used during hot reload scenarios,
    // but that's not a big deal. The classes will be removed on the next build.
    private static readonly HashSet<string> Classes = [];
    private static readonly HashSet<string> ProcessedUrls = [];
    private static readonly Lock Lock = new();
    
    private static void OnUpdate()
    {
        lock (Lock)
        {
            // Classes.Clear();
            // ProcessedUrls.Clear();
        }
    }

    // ClearCache should be the only one we need. Clearing with the rest might be problematic.
    // internal static void ClearCache(Type[]? _) => OnUpdate();
    // internal static void UpdateApplication(Type[]? _) => OnUpdate();
    // internal static void UpdateContent(string assemblyName, bool isApplicationProject, string relativePath, byte[] contents) => OnUpdate();

    public void AddClasses(string url, IEnumerable<string> classes)
    {
        lock (Lock)
        {
            if (ProcessedUrls.Contains(url))
            {
                return;
            }

            foreach (var cls in classes)
            {
                Classes.Add(cls);
            }

            ProcessedUrls.Add(url);
        }
    }

    public IReadOnlyCollection<string> GetClasses()
    {
        lock (Lock)
        {
            return Classes.ToList().AsReadOnly();
        }
    }

    public bool ShouldProcess(string url)
    {
        return true;
    }
}