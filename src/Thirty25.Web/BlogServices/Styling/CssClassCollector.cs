using System.Reflection.Metadata;
using Thirty25.Web.BlogServices.Styling;

[assembly: MetadataUpdateHandler(typeof(CssClassCollector))]

namespace Thirty25.Web.BlogServices.Styling;

internal class CssClassCollector
{
    private static readonly HashSet<string> Classes = [];
    private static readonly HashSet<string> ProcessedUrls = [];
    private static readonly Lock Lock = new();
    
    private static void OnUpdate()
    {
        lock (Lock)
        {
            Classes.Clear();
            ProcessedUrls.Clear();
        }
    }

    internal static void ClearCache(Type[]? _) => OnUpdate();
    internal static void UpdateApplication(Type[]? _) => OnUpdate();
    internal static void UpdateContent(string assemblyName, bool isApplicationProject, string relativePath, byte[] contents) => OnUpdate();

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
        lock (Lock)
        {
            return ProcessedUrls.Contains(url) == false;
        }
    }
}