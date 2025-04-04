﻿namespace BlazorStatic.Services;

/// <summary>
/// Watches specified directories for file system changes and triggers update actions.
/// This class monitors file creation, deletion, modification, and renaming events
/// in the specified directories and their subdirectories.
/// </summary>
public class BlazorStaticFileWatcher : IDisposable
{
    private bool _disposed;

    private readonly Dictionary<string, FileSystemWatcher> _watchers = new();
    private readonly List<Action> _updates = [];

    internal void Initialize(IEnumerable<string> contentToCopyList, Action onUpdate)
    {
        _updates.Add(onUpdate);
        SetupWatchers(contentToCopyList);
    }

    private void SetupWatchers(IEnumerable<string> paths)
    {
        foreach (var directoryPath in paths)
        {
            // Skip if path doesn't exist or we already have a watcher for it
            if (!Directory.Exists(directoryPath) || _watchers.ContainsKey(directoryPath))
            {
                continue;
            }

            try
            {
                var watcher = new FileSystemWatcher(directoryPath)
                {
                    NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.DirectoryName |
                                   NotifyFilters.CreationTime,
                    IncludeSubdirectories = true,
                    EnableRaisingEvents = true
                };

                // Add event handlers
                watcher.Changed += OnContentChanged;
                watcher.Created += OnContentChanged;
                watcher.Deleted += OnContentChanged;
                watcher.Renamed += OnContentRenamed;

                _watchers.Add(directoryPath, watcher);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }
        }
    }

    private void OnContentChanged(object sender, FileSystemEventArgs e)
    {
        foreach (var update in _updates)
        {
            update.Invoke();
        }
    }

    private void OnContentRenamed(object sender, RenamedEventArgs e)
    {
        foreach (var update in _updates)
        {
            update.Invoke();
        }
    }

    /// <summary>
    /// Releases all resources used by this instance.
    /// </summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Releases the unmanaged resources used by this instance and optionally releases the managed resources.
    /// </summary>
    /// <param name="disposing">true to release both managed and unmanaged resources; false to release only unmanaged resources.</param>
    private void Dispose(bool disposing)
    {
        if (_disposed) return;
        if (disposing)
        {
            // Dispose managed resources
            foreach (var watcher in _watchers.Values)
            {
                watcher.EnableRaisingEvents = false;
                watcher.Dispose();
            }

            _watchers.Clear();
        }

        _disposed = true;
    }

    /// <summary>
    /// Finalizer to ensure resources are cleaned up if Dispose is not called.
    /// </summary>
    ~BlazorStaticFileWatcher()
    {
        Dispose(false);
    }
}