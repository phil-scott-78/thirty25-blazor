using System.Web;

namespace Thirty25.Web.BlogServices.Roslyn;

internal class RoslynHighlighterService : IDisposable
{
    private readonly ILogger<RoslynHighlighterService> _logger;
    private readonly SyntaxHighlighter _highlighter;
    private readonly DocumentProcessor _documentProcessor;
    private readonly HighlightCache _cache;
    private readonly CodeFileWatcher _fileWatcher;
    private bool _disposed;

    public RoslynHighlighterService(string solutionFile, string examplePaths, ILogger<RoslynHighlighterService> logger)
    {
        _logger = logger;
        _highlighter = new SyntaxHighlighter();
        _cache = new HighlightCache();
        _documentProcessor = new DocumentProcessor(solutionFile, logger);
        _fileWatcher = new CodeFileWatcher(examplePaths, OnFileChanged, logger);
    }

    private void OnFileChanged(string filePath)
    {
        _logger.LogDebug("FileChanged: {filePath}", filePath);
        _documentProcessor.InvalidateFile(filePath);
    }

    public string HighlightExample(string xmlDocId, bool bodyOnly)
    {
        var code = _documentProcessor.GetCodeFragment(xmlDocId, bodyOnly);
        code = TextFormatter.NormalizeIndents(code);
        return _highlighter.Highlight(code);
    }

    public string Highlight(string codeContent, Language language = Language.CSharp)
    {
        return _cache.GetOrAdd(codeContent, () =>
            _highlighter.Highlight(HttpUtility.HtmlDecode(codeContent), language));
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (_disposed) return;

        if (disposing)
        {
            _fileWatcher.Dispose();
            _documentProcessor.Dispose();
            _highlighter.Dispose();
        }

        _disposed = true;
    }

    ~RoslynHighlighterService()
    {
        Dispose(false);
    }
}