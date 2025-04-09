using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;
using System.Web;
using BlazorStatic.Services;
using Microsoft.Build.Framework.Profiler;
using Microsoft.Build.Locator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Classification;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.MSBuild;
using Microsoft.CodeAnalysis.Text;
using Document = Microsoft.CodeAnalysis.Document;

namespace Thirty25.Web.BlogServices;

internal enum Language
{
    CSharp,
    VisualBasic,
}

internal class RoslynHighlighterService : IDisposable
{
    private readonly ILogger<RoslynHighlighterService> _logger;
    private readonly AdhocWorkspace _adHocWorkspace;
    private readonly MSBuildWorkspace _exampleWorkspace;
    private readonly Project _csharpProject;
    private readonly Project _vbProject;
    private bool _disposed;

    private readonly ConcurrentDictionary<int, string> _cache = new();
    private readonly ThreadSafePopulatedCache<string, (Document, TextSpan, SourceText)> _roslynCache;
    private readonly FileSystemWatcher _fileSystemWatch;

    private readonly HashSet<string> _pendingChanges = new(StringComparer.OrdinalIgnoreCase);
    private readonly Lock _changeLock = new();

    private void OnFileChanged(string filePath)
    {
        _logger.LogDebug("FileChanged: {filePath}", filePath);
        lock (_changeLock)
        {
            _pendingChanges.Add(filePath);
        }

        _roslynCache.Invalidate();
    }

    public RoslynHighlighterService(string solutionFile, string examplePaths, ILogger<RoslynHighlighterService> logger)
    {
        _logger = logger;
        _adHocWorkspace = new AdhocWorkspace();
        _csharpProject =
            _adHocWorkspace.CurrentSolution.AddProject("csProjectName", "assemblyName", LanguageNames.CSharp);
        _vbProject =
            _adHocWorkspace.CurrentSolution.AddProject("vbProjectName", "assemblyName", LanguageNames.VisualBasic);

        _exampleWorkspace = MSBuildWorkspace.Create();
        _exampleWorkspace.LoadMetadataForReferencedProjects = true;
        _exampleWorkspace.WorkspaceFailed += (_, args) =>
        {
            _logger.LogWarning("Workspace load issue: {message} (DiagnosticKind: {kind})", args.Diagnostic.Message, args.Diagnostic.Kind);
        };
        
        _roslynCache = new ThreadSafePopulatedCache<string, (Document, TextSpan, SourceText)>(
            async () =>
            {
                var solution = _exampleWorkspace.CurrentSolution.ProjectIds.Count == 0
                    ? await _exampleWorkspace.OpenSolutionAsync(solutionFile)
                    : await ApplyPendingChangesToSolutionAsync(_exampleWorkspace.CurrentSolution);
                
                return await GetAllTypesAndMethodsInSolutionAsync(solution);
            });

        var path = new DirectoryInfo(examplePaths);

        _logger.LogInformation("Watching {path} for code changes.", path.FullName);
        _fileSystemWatch = new FileSystemWatcher(path.FullName, "*.cs")
        {
            IncludeSubdirectories = true,
            EnableRaisingEvents = true
        };

        _fileSystemWatch.Changed += (_, e) => OnFileChanged(e.FullPath);
        _fileSystemWatch.Created += (_, e) => OnFileChanged(e.FullPath);
        _fileSystemWatch.Deleted += (_, e) => OnFileChanged(e.FullPath);
        _fileSystemWatch.Renamed += (_, e) => OnFileChanged(e.FullPath);
    }


    private async Task<Solution> ApplyPendingChangesToSolutionAsync(Solution solution)
    {
        List<string> changedFiles;
        lock (_changeLock)
        {
            changedFiles = _pendingChanges.ToList();
            _pendingChanges.Clear();
        }

        foreach (var filePath in changedFiles)
        {
            var docIds = solution.GetDocumentIdsWithFilePath(filePath);
            foreach (var docId in docIds)
            {
                if (File.Exists(filePath))
                {
                    var text = SourceText.From(await File.ReadAllTextAsync(filePath));
                    solution = solution.WithDocumentText(docId, text);
                }
                else
                {
                    solution = solution.RemoveDocument(docId);
                }
            }
        }

        _exampleWorkspace.TryApplyChanges(solution);
        return solution;
    }

    public string HighlightExample(string xmlDocId, bool bodyOnly)
    {
        var code = RunSync(async () =>
        {
            var sanitizedXmlDocId = DocIdSanitizer.SanitizeXmlDocId(xmlDocId);
            _logger.LogInformation("Looking up {xmlDocId}.", sanitizedXmlDocId);
            if (await _roslynCache.TryGetValueAsync(sanitizedXmlDocId) is not { Found: true, Value: var (document, originalSpan, sourceText) })
            {
                _logger.LogWarning("Failed to find {sanitizedXmlDocId}", sanitizedXmlDocId);
                return "Code not found for specified documentation ID.";
            }

            return await ExtractCodeFragmentAsync(document, originalSpan, sourceText, bodyOnly);
        });

        code = NormalizeIndents(code);
        return Highlight(code);
    }

    private static async Task<string> ExtractCodeFragmentAsync(Document document, TextSpan originalSpan,
        SourceText sourceText, bool bodyOnly)
    {
        // Check if this is a method and handle bodyOnly if it is
        var syntaxTree = await document.GetSyntaxTreeAsync() ?? throw new NullReferenceException();
        var syntaxRoot = await syntaxTree.GetRootAsync();
        var nodeAtSpan = syntaxRoot.FindNode(originalSpan);

        if (nodeAtSpan is not MethodDeclarationSyntax methodNode)
        {
            return sourceText.GetSubText(originalSpan).ToString();
        }

        if (!bodyOnly)
        {
            return sourceText.GetSubText(originalSpan).ToString();
        }

        // For bodyOnly, we only want the body of the method, not the signature or braces
        if (methodNode.Body == null)
        {
            return methodNode.ExpressionBody != null
                ? sourceText.GetSubText(methodNode.ExpressionBody.Span).ToString()
                : sourceText.GetSubText(originalSpan).ToString();
        }

        // Get the inner content of the body, excluding the opening and closing braces
        var bodySpan = TextSpan.FromBounds(
            methodNode.Body.OpenBraceToken.Span.End,
            methodNode.Body.CloseBraceToken.SpanStart);

        return sourceText.GetSubText(bodySpan).ToString();
    }

    private static string NormalizeIndents(string code)
    {
        if (string.IsNullOrWhiteSpace(code))
            return code;

        var lines = code.TrimEnd().Split('\n');

        // Find the first non-empty line
        var firstNonEmptyIndex = Array.FindIndex(lines, line => !string.IsNullOrWhiteSpace(line));
        if (firstNonEmptyIndex < 0)
            return string.Empty; // All lines are empty or whitespace

        // Calculate the common indent from the first non-empty line
        var commonIndent = lines[firstNonEmptyIndex].Length - lines[firstNonEmptyIndex].TrimStart(' ').Length;

        // Process each line from the first non-empty line
        for (var i = firstNonEmptyIndex; i < lines.Length; i++)
        {
            if (!string.IsNullOrWhiteSpace(lines[i]))
            {
                var currentIndent = Math.Min(commonIndent, lines[i].Length - lines[i].TrimStart(' ').Length);
                lines[i] = lines[i][currentIndent..];
            }
        }

        // Join only the lines from the first non-empty one
        return string.Join('\n', lines.Skip(firstNonEmptyIndex));
    }

    public string Highlight(string codeContent, Language language = Language.CSharp)
    {
        // Calculate a hash for the content to use as cache key
        var contentHash = codeContent.GetHashCode();

        var highlightedCode = _cache.GetOrAdd(contentHash, _ =>
        {
            var project = language switch
            {
                Language.CSharp => _csharpProject,
                Language.VisualBasic => _vbProject,
                _ => throw new NotSupportedException($"Language {language} is not supported.")
            };

            return RunSync(() =>
            {
                // HTML decode the content to get actual code
                var decodedContent = HttpUtility.HtmlDecode(codeContent);
                return HighlightContent(decodedContent, project);
            });
        });

        return $"<pre><code>{highlightedCode}</code></pre>";
    }

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

    private async Task<Dictionary<string, (Document document, TextSpan textSpan, SourceText sourceText)>>
        GetAllTypesAndMethodsInSolutionAsync(Solution solution)
    {
        var sw = Stopwatch.StartNew();
        var result = new Dictionary<string, (Document, TextSpan, SourceText)>();

        foreach (var project in solution.Projects)
        {
            _logger.LogInformation("Getting types and methods {project}", project.FilePath);
            if ((project.FilePath?.Contains("blog-projects") ?? false) == false)
            {
                _logger.LogInformation("Skipping {project}", project.FilePath);
                continue;
            }

            await ProcessProjectDocumentsAsync(project, result);
        }

        sw.Stop();
        _logger.LogInformation("Rebuilt roslyn cache in {elapsed}", sw.Elapsed);

        return result;
    }

    private async Task ProcessProjectDocumentsAsync(Project project,
        Dictionary<string, (Document document, TextSpan textSpan, SourceText sourceText)> result)
    {
        foreach (var document in project.Documents)
        {
            // Skip documents that aren't C# code files
            if (!document.SupportsSyntaxTree)
                continue;

            var syntaxTree = await document.GetSyntaxTreeAsync() ?? throw new NullReferenceException();
            var semanticModel = await document.GetSemanticModelAsync() ?? throw new NullReferenceException();
            var sourceText = await document.GetTextAsync();
            var rootSyntaxNode = await syntaxTree.GetRootAsync();

            ProcessTypeDeclarationsAsync(document, rootSyntaxNode, semanticModel, sourceText, result);
            ProcessMethodDeclarationsAsync(document, rootSyntaxNode, semanticModel, sourceText, result);
        }
    }

    private void ProcessTypeDeclarationsAsync(Document document, SyntaxNode rootSyntaxNode,
        SemanticModel semanticModel, SourceText sourceText,
        Dictionary<string, (Document document, TextSpan textSpan, SourceText sourceText)> result)
    {
        // Get all type declarations (classes, structs, interfaces, etc.)
        var typeDeclarations = rootSyntaxNode.DescendantNodes()
            .Where(n => n is TypeDeclarationSyntax)
            .Cast<TypeDeclarationSyntax>();

        foreach (var typeDeclaration in typeDeclarations)
        {
            var typeSymbol = ModelExtensions.GetDeclaredSymbol(semanticModel, typeDeclaration);
            if (typeSymbol == null) continue;

            var xmlDocId = typeSymbol.GetDocumentationCommentId();
            if (string.IsNullOrEmpty(xmlDocId)) continue;

            xmlDocId = DocIdSanitizer.SanitizeXmlDocId(xmlDocId);

            var textSpan = CreateExtendedTextSpan(typeDeclaration);
            var tuple = (document, textSpan, sourceText);

            // Add to dictionary if not already present
            result.TryAdd(xmlDocId, tuple);
        }
    }

    private void ProcessMethodDeclarationsAsync(Document document, SyntaxNode rootSyntaxNode,
        SemanticModel semanticModel, SourceText sourceText,
        Dictionary<string, (Document document, TextSpan textSpan, SourceText sourceText)> result)
    {
        // Get all method declarations
        var methodDeclarations = rootSyntaxNode.DescendantNodes()
            .Where(n => n is MethodDeclarationSyntax)
            .Cast<MethodDeclarationSyntax>();

        foreach (var methodDeclaration in methodDeclarations)
        {
            var methodSymbol = ModelExtensions.GetDeclaredSymbol(semanticModel, methodDeclaration);
            if (methodSymbol == null) continue;

            var xmlDocId = methodSymbol.GetDocumentationCommentId();
            if (string.IsNullOrEmpty(xmlDocId)) continue;

            xmlDocId = DocIdSanitizer.SanitizeXmlDocId(xmlDocId);
            
            var textSpan = CreateExtendedTextSpan(methodDeclaration);

            // Add to dictionary if not already present
            result.TryAdd(xmlDocId, (document, textSpan, sourceText));
        }
    }

    private static TextSpan CreateExtendedTextSpan(CSharpSyntaxNode syntaxNode)
    {
        // Get the leading trivia and find whitespace
        var leadingTrivia = syntaxNode.GetLeadingTrivia();
        var leadingWhitespaceLength = leadingTrivia
            .Where(t => t.IsKind(SyntaxKind.WhitespaceTrivia))
            .Sum(t => t.Span.Length);

        // Create a new text span that includes the leading whitespace
        var extendedStartPosition = syntaxNode.SpanStart - leadingWhitespaceLength;
        var extendedLength = syntaxNode.Span.Length + leadingWhitespaceLength;

        return new TextSpan(extendedStartPosition, extendedLength);
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
            _adHocWorkspace.Dispose();
            _exampleWorkspace.Dispose();
        }

        _disposed = true;
    }

    ~RoslynHighlighterService()
    {
        Dispose(false);
    }

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