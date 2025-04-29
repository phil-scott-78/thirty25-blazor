using System.Text;
using System.Web;
using BlazorStatic.Services.Infrastructure;
using Microsoft.Extensions.Logging;

namespace BlazorStatic.Services.Content.Roslyn;

/// <summary>
/// A service for providing syntax highlighting for code blocks using Roslyn.
/// </summary>
public class RoslynHighlighterService : IDisposable
{
    private readonly ILogger<RoslynHighlighterService> _logger;
    private readonly SyntaxHighlighter _highlighter;
    private readonly DocumentProcessor? _documentProcessor;
    private readonly HighlightCache _cache;
    private readonly BlazorFileWatcher _fileWatcher;
    private bool _disposed;

    /// <summary>
    /// Provides functionality for syntax highlighting of code using Roslyn.
    /// </summary>
    public RoslynHighlighterService(RoslynHighlighterOptions options, ILogger<RoslynHighlighterService> logger, BlazorFileWatcher fileWatcher)
    {
        _logger = logger;
        _highlighter = new SyntaxHighlighter();
        _cache = new HighlightCache();
        _fileWatcher = fileWatcher;

        if (options.ConnectedSolution != null)
        {
            _documentProcessor = new DocumentProcessor(options.ConnectedSolution.SolutionPath, logger);
            _fileWatcher.AddPathWatch(options.ConnectedSolution.ProjectsPath, "*.cs", OnFileChanged);
        }
    }

    private void OnFileChanged(string filePath)
    {
        _logger.LogDebug("FileChanged: {filePath}", filePath);
        _documentProcessor?.InvalidateFile(filePath);
    }

    internal string HighlightExample(string xmlDocIds, bool bodyOnly)
    {
        if (_documentProcessor == null)
        {
            throw new InvalidOperationException(
                "Highlighting by XmlDocId is only supported when ConnectedSolution is configured");
        }

        var ids = xmlDocIds
            .ReplaceLineEndings()
            .Split(Environment.NewLine,
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        var sb = new StringBuilder();

        foreach (var xmlDocId in ids)
        {
            var code = _documentProcessor.GetCodeFragment(xmlDocId, bodyOnly);
            code = TextFormatter.NormalizeIndents(code);
            var highlightExample = _highlighter.Highlight(code);
            sb.Append(highlightExample.TrimEnd());
            sb.AppendLine();
            sb.AppendLine();
        }

        return $"<pre><code>{sb.ToString().TrimEnd()}</code></pre>";
    }

    internal string Highlight(string codeContent, Language language = Language.CSharp)
    {
        var highlightExample = _cache.GetOrAdd(codeContent, () =>
            _highlighter.Highlight(HttpUtility.HtmlDecode(codeContent), language));
        return $"<pre><code>{highlightExample}</code></pre>";
    }

    /// <inheritdoc />
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
            _fileWatcher.Dispose();
            _documentProcessor?.Dispose();
            _highlighter.Dispose();
        }

        _disposed = true;
    }

    /// <summary>
    /// Finalizes an instance of the <see cref="RoslynHighlighterService"/> class.
    /// </summary>
    ~RoslynHighlighterService()
    {
        Dispose(false);
    }
}