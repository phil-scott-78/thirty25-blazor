using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Classification;
using Microsoft.CodeAnalysis.Text;

namespace Thirty25.Web.BlogServices.Roslyn;

internal enum Language
{
    CSharp,
    VisualBasic,
}

internal class SyntaxHighlighter : IDisposable
{
    private readonly AdhocWorkspace _adHocWorkspace;
    private readonly Project _csharpProject;
    private readonly Project _vbProject;
    private bool _disposed;

    public SyntaxHighlighter()
    {
        _adHocWorkspace = new AdhocWorkspace();
        _csharpProject = _adHocWorkspace.CurrentSolution.AddProject("csProjectName", "assemblyName", LanguageNames.CSharp);
        _vbProject = _adHocWorkspace.CurrentSolution.AddProject("vbProjectName", "assemblyName", LanguageNames.VisualBasic);
    }

    public string Highlight(string codeContent, Language language = Language.CSharp)
    {
        var project = language switch
        {
            Language.CSharp => _csharpProject,
            Language.VisualBasic => _vbProject,
            _ => throw new NotSupportedException($"Language {language} is not supported.")
        };

        var highlightedCode = AsyncHelpers.RunSync(() => HighlightContent(codeContent, project));
        return $"<pre><code>{highlightedCode}</code></pre>";
    }

    // Keep all the highlighting methods from original class:
    private static async Task<string> HighlightContent(string codeContent, Project project)
    {
        // Create a randomish name so we don't have a collision
        var filename = $"name.{codeContent.GetHashCode()}.{Environment.CurrentManagedThreadId}.cs";
        var document = project.AddDocument(filename, codeContent);
        var text = await document.GetTextAsync();
        var textBounds = TextSpan.FromBounds(0, text.Length);
        return await HighlightTextSpan(document, textBounds, text);
    }

    private static async Task<string> HighlightTextSpan(Document document, TextSpan textSpan, SourceText fullText)
    {
        // Get the subtext from the full source text, limited to our text span
        var targetText = fullText.GetSubText(textSpan);

        // Get classified spans but only for our specific text span
        var classifiedSpans = await Classifier.GetClassifiedSpansAsync(document, textSpan);

        // Adjust the classified spans to be relative to the start of our text span
        var adjustedSpans = AdjustClassifiedSpans(textSpan, classifiedSpans);

        // Create ranges with the adjusted spans
        var ranges = CreateRangesFromSpans(targetText, adjustedSpans);

        // Fill in gaps (whitespace) based on the target text
        ranges = FillGaps(targetText, ranges);

        return BuildHighlightedOutput(ranges);
    }

    private static IEnumerable<ClassifiedSpan> AdjustClassifiedSpans(TextSpan textSpan,
        IEnumerable<ClassifiedSpan> classifiedSpans)
    {
        return classifiedSpans.Select(span =>
        {
            var adjustedStart = span.TextSpan.Start - textSpan.Start;
            var length = span.TextSpan.Length;
            var adjustedSpan = new TextSpan(adjustedStart, length);
            return new ClassifiedSpan(span.ClassificationType, adjustedSpan);
        });
    }


    private static IEnumerable<Range> CreateRangesFromSpans(SourceText targetText,
        IEnumerable<ClassifiedSpan> adjustedSpans)
    {
        return adjustedSpans.Select(span =>
        {
            var rangeText = targetText.GetSubText(span.TextSpan).ToString();
            return new Range(span, rangeText);
        });
    }

    private static string BuildHighlightedOutput(IEnumerable<Range> ranges)
    {
        var sb = new StringBuilder();

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
                // There's a gap between the current position and the start of this range
                yield return new Range(whitespaceClassification, TextSpan.FromBounds(current, start), text);
            }

            if (previous == null || range.TextSpan != previous.TextSpan)
            {
                yield return range;
            }

            previous = range;
            current = range.TextSpan.End;
        }

        // If there's a gap at the end, fill it
        if (current < text.Length)
        {
            yield return new Range(whitespaceClassification, TextSpan.FromBounds(current, text.Length), text);
        }
    }

    private class Range
    {
        private readonly ClassifiedSpan _classifiedSpan;
        public string Text { get; }

        public Range(ClassifiedSpan classifiedSpan, string text)
        {
            _classifiedSpan = classifiedSpan;
            Text = text;
        }

        public Range(string classification, TextSpan span, SourceText text)
            : this(classification, span, text.GetSubText(span).ToString())
        {
        }

        private Range(string classification, TextSpan span, string text)
            : this(new ClassifiedSpan(classification, span), text)
        {
        }

        public string ClassificationType => _classifiedSpan.ClassificationType;

        public TextSpan TextSpan => _classifiedSpan.TextSpan;
    }

    private static string ClassificationTypeToStarryNightClass(string classificationType)
    {
        return classificationType switch
        {
            // Variable/symbol
            ClassificationTypeNames.Identifier => "pl-v",

            // Variable
            ClassificationTypeNames.LocalName or ClassificationTypeNames.ParameterName => "",

            // Constant/property
            ClassificationTypeNames.PropertyName or ClassificationTypeNames.EnumMemberName
                or ClassificationTypeNames.FieldName => "pl-c1",

            // Namespace
            ClassificationTypeNames.ClassName or ClassificationTypeNames.StructName
                or ClassificationTypeNames.RecordClassName or ClassificationTypeNames.RecordStructName
                or ClassificationTypeNames.InterfaceName or ClassificationTypeNames.DelegateName
                or ClassificationTypeNames.EnumName or ClassificationTypeNames.ModuleName => "pl-n",

            // Entity name (types)
            ClassificationTypeNames.TypeParameterName => "pl-e",

            // Keyword, used for function names in many starry-night themes
            ClassificationTypeNames.MethodName or ClassificationTypeNames.ExtensionMethodName => "pl-k",

            // Comment
            ClassificationTypeNames.Comment => "pl-c",

            // Keyword
            ClassificationTypeNames.Keyword or ClassificationTypeNames.ControlKeyword
                or ClassificationTypeNames.PreprocessorKeyword => "pl-k",

            // String
            ClassificationTypeNames.StringLiteral or ClassificationTypeNames.VerbatimStringLiteral => "pl-s",

            // Constant (numbers)
            ClassificationTypeNames.NumericLiteral => "pl-c1",

            // Keyword operators and separators
            ClassificationTypeNames.Operator or ClassificationTypeNames.StringEscapeCharacter => "pl-kos",

            // Punctuation delimiter string
            ClassificationTypeNames.Punctuation => "pl-pds",
            ClassificationTypeNames.StaticSymbol => string.Empty,

            // Comments in starry-night
            ClassificationTypeNames.XmlDocCommentComment or ClassificationTypeNames.XmlDocCommentDelimiter
                or ClassificationTypeNames.XmlDocCommentName or ClassificationTypeNames.XmlDocCommentText
                or ClassificationTypeNames.XmlDocCommentAttributeName
                or ClassificationTypeNames.XmlDocCommentAttributeQuotes
                or ClassificationTypeNames.XmlDocCommentAttributeValue
                or ClassificationTypeNames.XmlDocCommentEntityReference
                or ClassificationTypeNames.XmlDocCommentProcessingInstruction
                or ClassificationTypeNames.XmlDocCommentCDataSection => "pl-c",
            _ => "pl-" + classificationType.ToLower().Replace(" ", "-")
        };
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
            _adHocWorkspace.Dispose();
        }
    
        _disposed = true;
    }

    ~SyntaxHighlighter()
    {
        Dispose(false);
    }


}