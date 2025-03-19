using BlazorStatic;
using System.Reflection.Metadata;

[assembly: MetadataUpdateHandler(typeof(HotReloadManager))]

namespace BlazorStatic;

/// <summary>
///     Used for subscribing to the hotReload update event, which re-generates the outputed content.
/// </summary>
internal sealed class HotReloadManager
{
    /// <summary>
    ///     Event that is raised whenever any update occurs in the hot reload system.
    /// </summary>
    private static readonly List<Action> UpdateCallbacks = [];

    public static void Subscribe(Action action)
    {
        UpdateCallbacks.Add(action);
    }

    public static void Unsubscribe(Action action)
    {
        UpdateCallbacks.Remove(action);
    }

    /// <summary>
    ///     Raises the Update event.
    /// </summary>
    private static void OnUpdate()
    {
        foreach(var update in UpdateCallbacks)
        {
            update.Invoke();
        }
    }

    internal static void ClearCache(Type[]? _)
    {
        OnUpdate();
    }

    internal static void UpdateApplication(Type[]? _)
    {
        OnUpdate();
    }

    internal static void UpdateContent(string assemblyName, bool isApplicationProject, string relativePath,
        byte[] contents)
    {
        OnUpdate();
    }
}
