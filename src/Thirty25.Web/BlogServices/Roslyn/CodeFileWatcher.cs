namespace Thirty25.Web.BlogServices.Roslyn;

internal sealed class CodeFileWatcher : IDisposable
{
    private readonly FileSystemWatcher _fileSystemWatch;
    private bool _disposed;

    public CodeFileWatcher(string path, Action<string> onFileChanged, ILogger logger)
    {
        var directoryInfo = new DirectoryInfo(path);

        logger.LogDebug("Watching {path} for code changes.", directoryInfo.FullName);
        _fileSystemWatch = new FileSystemWatcher(directoryInfo.FullName, "*.cs")
        {
            IncludeSubdirectories = true,
            EnableRaisingEvents = true
        };

        _fileSystemWatch.Changed += (_, e) => onFileChanged(e.FullPath);
        _fileSystemWatch.Created += (_, e) => onFileChanged(e.FullPath);
        _fileSystemWatch.Deleted += (_, e) => onFileChanged(e.FullPath);
        _fileSystemWatch.Renamed += (_, e) => onFileChanged(e.FullPath);
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    private void Dispose(bool disposing)
    {
        if (_disposed) return;

        if (disposing)
        {
            // Dispose managed resources
            _fileSystemWatch.Dispose();
        }

        // Set flag to prevent redundant calls
        _disposed = true;
    }

    // Finalizer
    ~CodeFileWatcher()
    {
        Dispose(false);
    }
}