using System.Collections.Concurrent;
using System.Reflection.Metadata;
using System.Text;
using System.Text.RegularExpressions;
using System.Web;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Classification;
using Microsoft.CodeAnalysis.Text;
using Thirty25.Web;

// this will give the highlighter service a callback to the ClearCache method
// during any compilations that occur that would require the cache being cleared.
[assembly: MetadataUpdateHandler(typeof(RoslynHighlighterService))]

namespace Thirty25.Web;

internal partial class RoslynHighlighterService : IDisposable
{
    private readonly AdhocWorkspace _workspace;
    private readonly Project _project;
    private bool _disposed;
    private static readonly ConcurrentDictionary<int, string> Cache = new();

    public RoslynHighlighterService()
    {
        _workspace = new AdhocWorkspace();
        _project = _workspace.CurrentSolution.AddProject("projectName", "assemblyName", LanguageNames.CSharp);
    }
    
    internal static void ClearCache(Type[]? _)
    {
        Cache.Clear();
    }

    public string Highlight(string htmlCode)
    {
        var highlighted = false;

        // Process each match
        var result = CSharpLanguageBlockRegEx().Replace(htmlCode, match =>
        {
            highlighted = true;
            var openingTagStart = match.Groups[1].Value;
            var openingTagEnd = match.Groups[2].Value;
            var codeContent = match.Groups[3].Value;
            var closingTags = match.Groups[4].Value;
           
            // Calculate a hash for the content to use as cache key
            var contentHash = codeContent.GetHashCode();
            
            var highlightedCode = Cache.GetOrAdd(contentHash, _ =>
            {
                return RunSync(() =>
                {
                    // HTML decode the content to get actual code
                    var decodedContent = HttpUtility.HtmlDecode(codeContent);
                    return HighlightContent(decodedContent, _project);
                });
            });

            // Remove language-csharp and replace with language-none so prism.js skips it
            return $"{openingTagStart}language-none{openingTagEnd}{highlightedCode}{closingTags}";
        });

        return highlighted ? result : htmlCode;
    }

    private static async Task<string> HighlightContent(string codeContent, Project project)
    {
        // Create a randomish name so we don't have a collision
        var filename = $"name.{codeContent.GetHashCode()}.{Environment.CurrentManagedThreadId}.cs";
        var document = project.AddDocument(filename, codeContent);
        var text = await document.GetTextAsync();
        var classifiedSpans = await Classifier.GetClassifiedSpansAsync(document, TextSpan.FromBounds(0, text.Length));
        var ranges = classifiedSpans.Select(classifiedSpan =>
            new Range(classifiedSpan, text.GetSubText(classifiedSpan.TextSpan).ToString()));

        // Fill in gaps (whitespace)
        ranges = FillGaps(text, ranges);

        var sb = new StringBuilder(codeContent.Length);

        foreach (var range in ranges)
        {
            var cssClass = ClassificationTypeToStarryNightClass(range.ClassificationType);
            if (string.IsNullOrWhiteSpace(cssClass))
            {
                sb.Append(range.Text);
            }
            else
            {
                // Include the prism css class and roslyn classification
                sb.Append($"""
                           <span class="token {cssClass} roslyn-{range.ClassificationType.Replace(" ", "-")}">{range.Text}</span>
                           """);
            }
        }

        return sb.ToString();
    }

    private static IEnumerable<Range> FillGaps(SourceText text, IEnumerable<Range> ranges)
    {
        const string whitespaceClassification = "";
        var current = 0;
        Range? previous = null;

        foreach (var range in ranges)
        {
            var start = range.TextSpan.Start;
            if (start > current)
            {
                yield return new Range(whitespaceClassification, TextSpan.FromBounds(current, start), text);
            }

            if (previous == null || range.TextSpan != previous.TextSpan)
            {
                yield return range;
            }

            previous = range;
            current = range.TextSpan.End;
        }

        if (current < text.Length)
        {
            yield return new Range(whitespaceClassification, TextSpan.FromBounds(current, text.Length), text);
        }
    }

    private class Range(ClassifiedSpan classifiedSpan, string text)
    {
        private ClassifiedSpan ClassifiedSpan { get; } = classifiedSpan;
        public string Text { get; } = text;

        public Range(string classification, TextSpan span, SourceText text) : this(classification, span, text.GetSubText(span).ToString()) { }
        private Range(string classification, TextSpan span, string text) : this(new ClassifiedSpan(classification, span), text) { }

        public string ClassificationType => ClassifiedSpan.ClassificationType;

        public TextSpan TextSpan => ClassifiedSpan.TextSpan;
    }

    private static string ClassificationTypeToStarryNightClass(string rangeClassificationType)
    {
        switch (rangeClassificationType)
        {
            case ClassificationTypeNames.Identifier:
                return "pl-v"; // Variable/symbol in starry-night
            case ClassificationTypeNames.LocalName:
            case ClassificationTypeNames.ParameterName:
                return ""; // Variable
            case ClassificationTypeNames.PropertyName:
            case ClassificationTypeNames.EnumMemberName:
            case ClassificationTypeNames.FieldName:
                return "pl-c1"; // Constant/property
            case ClassificationTypeNames.ClassName:
            case ClassificationTypeNames.StructName:
            case ClassificationTypeNames.RecordClassName:
            case ClassificationTypeNames.RecordStructName:
            case ClassificationTypeNames.InterfaceName:
            case ClassificationTypeNames.DelegateName:
            case ClassificationTypeNames.EnumName:
            case ClassificationTypeNames.ModuleName:
                return "pl-n"; // Namespace
            case ClassificationTypeNames.TypeParameterName:
                return "pl-e"; // Entity name (types)
            case ClassificationTypeNames.MethodName:
            case ClassificationTypeNames.ExtensionMethodName:
                return "pl-k"; // Keyword, used for function names in many starry-night themes
            case ClassificationTypeNames.Comment:
                return "pl-c"; // Comment
            case ClassificationTypeNames.Keyword:
            case ClassificationTypeNames.ControlKeyword:
            case ClassificationTypeNames.PreprocessorKeyword:
                return "pl-k"; // Keyword
            case ClassificationTypeNames.StringLiteral:
            case ClassificationTypeNames.VerbatimStringLiteral:
                return "pl-s"; // String
            case ClassificationTypeNames.NumericLiteral:
                return "pl-c1"; // Constant (numbers)
            case ClassificationTypeNames.Operator:
            case ClassificationTypeNames.StringEscapeCharacter:
                return "pl-kos"; // Keyword operators and separators
            case ClassificationTypeNames.Punctuation:
                return "pl-pds"; // Punctuation delimiter string
            case ClassificationTypeNames.StaticSymbol:
                return string.Empty;
            case ClassificationTypeNames.XmlDocCommentComment:
            case ClassificationTypeNames.XmlDocCommentDelimiter:
            case ClassificationTypeNames.XmlDocCommentName:
            case ClassificationTypeNames.XmlDocCommentText:
            case ClassificationTypeNames.XmlDocCommentAttributeName:
            case ClassificationTypeNames.XmlDocCommentAttributeQuotes:
            case ClassificationTypeNames.XmlDocCommentAttributeValue:
            case ClassificationTypeNames.XmlDocCommentEntityReference:
            case ClassificationTypeNames.XmlDocCommentProcessingInstruction:
            case ClassificationTypeNames.XmlDocCommentCDataSection:
                return "pl-c"; // Comments in starry-night
            default:
                // Replace spaces with hyphens and prefix with pl- to maintain convention
                return "pl-" + rangeClassificationType.ToLower().Replace(" ", "-");
        }
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
            _workspace.Dispose();
        }

        // Free unmanaged resources
        // Set large fields to null

        _disposed = true;
    }

    // Finalizer (only if you have unmanaged resources)
    ~RoslynHighlighterService()
    {
        Dispose(false);
    }

    [GeneratedRegex("""(<pre\s*>?\s*<code\s+class\s*=\s*['"])language-csharp(['"].*?>)(.*?)(<\/code>\s*<\/pre>)""",
        RegexOptions.Singleline)]
    private static partial Regex CSharpLanguageBlockRegEx();


    private static readonly TaskFactory TaskFactory = new(CancellationToken.None,
        TaskCreationOptions.None,
        TaskContinuationOptions.None,
        TaskScheduler.Default);

    private static TResult RunSync<TResult>(Func<Task<TResult>> func,
        CancellationToken cancellationToken = default(CancellationToken))
        => TaskFactory
            .StartNew(func, cancellationToken)
            .Unwrap()
            .GetAwaiter()
            .GetResult();
}