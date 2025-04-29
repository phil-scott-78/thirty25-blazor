using System.Diagnostics;
using BlazorStatic.Services;
using Microsoft.Build.Locator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.MSBuild;
using Microsoft.CodeAnalysis.Text;

namespace Thirty25.Web.BlogServices.Roslyn;

internal class DocumentProcessor : IDisposable
{
    private readonly ILogger _logger;
    private readonly MSBuildWorkspace _workspace;
    private readonly ThreadSafePopulatedCache<string, (Document, TextSpan, SourceText)> _roslynCache;
    private readonly HashSet<string> _pendingChanges = new(StringComparer.OrdinalIgnoreCase);
    private readonly Lock _changeLock = new();
    private bool _disposed;

    public DocumentProcessor(string solutionFile, ILogger logger)
    {
        _logger = logger;

        // Setup MSBuild workspace
        var msBuildInstance = MSBuildLocator.QueryVisualStudioInstances().First();
        MSBuildLocator.RegisterInstance(msBuildInstance);

        _workspace = MSBuildWorkspace.Create();
        _workspace.LoadMetadataForReferencedProjects = true;
        _workspace.WorkspaceFailed += (_, args) =>
        {
            _logger.LogWarning("Workspace load issue: {message} (DiagnosticKind: {kind})",
                args.Diagnostic.Message, args.Diagnostic.Kind);
        };

        _roslynCache = new ThreadSafePopulatedCache<string, (Document, TextSpan, SourceText)>(async () =>
        {
            var solution = _workspace.CurrentSolution.ProjectIds.Count == 0
                ? await _workspace.OpenSolutionAsync(solutionFile)
                : await ApplyPendingChangesToSolutionAsync(_workspace.CurrentSolution);

            return await GetAllTypesAndMethodsInSolutionAsync(solution);
        });
    }

    public void InvalidateFile(string filePath)
    {
        lock (_changeLock)
        {
            _pendingChanges.Add(filePath);
        }

        _roslynCache.Invalidate();
    }

    public string GetCodeFragment(string xmlDocId, bool bodyOnly)
    {
        return AsyncHelpers.RunSync(async () =>
        {
            var sanitizedXmlDocId = DocIdSanitizer.SanitizeXmlDocId(xmlDocId);

            if (await _roslynCache.TryGetValueAsync(sanitizedXmlDocId) is
                { Found: true, Value: var (document, originalSpan, sourceText) })
            {
                return await ExtractCodeFragmentAsync(document, originalSpan, sourceText, bodyOnly);
            }

            _logger.LogWarning("Failed to find {sanitizedXmlDocId}", sanitizedXmlDocId);
            return "Code not found for specified documentation ID.";
        });
    }

    // Keep all the remaining methods from original RoslynHighlighterService:
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

        _workspace.TryApplyChanges(solution);
        return solution;
    }

    private static async Task<string> ExtractCodeFragmentAsync(Document document, TextSpan originalSpan,
        SourceText sourceText, bool bodyOnly)
    {
        // If we don't want body only, just return the original text
        if (!bodyOnly)
        {
            return sourceText.GetSubText(originalSpan).ToString();
        }

        // Find the node at the span
        var syntaxTree = await document.GetSyntaxTreeAsync() ?? throw new NullReferenceException();
        var syntaxRoot = await syntaxTree.GetRootAsync();
        var nodeAtSpan = syntaxRoot.FindNode(originalSpan);

        return nodeAtSpan switch
        {
            // Handle method declarations
            MethodDeclarationSyntax methodNode => ExtractMethodBodyContent(methodNode, sourceText, originalSpan),
            // Handle class declarations
            ClassDeclarationSyntax classNode => ExtractClassBodyContent(classNode, sourceText, originalSpan),
            // For any other node type, return the original span
            _ => sourceText.GetSubText(originalSpan).ToString()
        };
    }

    private static string ExtractMethodBodyContent(MethodDeclarationSyntax methodNode, SourceText sourceText, TextSpan originalSpan)
    {
        // For expression body, return just the expression
        if (methodNode.ExpressionBody != null)
        {
            return sourceText.GetSubText(methodNode.ExpressionBody.Span).ToString();
        }

        // Fallback to the original span if neither is available
        if (methodNode.Body == null)
        {
            return sourceText.GetSubText(originalSpan).ToString();
        }
        
        // For block body, return the content between braces
        var bodySpan = TextSpan.FromBounds(
            methodNode.Body.OpenBraceToken.Span.End,
            methodNode.Body.CloseBraceToken.SpanStart);

        return sourceText.GetSubText(bodySpan).ToString();
    }

    private static string ExtractClassBodyContent(ClassDeclarationSyntax classNode, SourceText sourceText, TextSpan originalSpan)
    {
        // Fallback to the original span if we can't find valid braces
        if (classNode.OpenBraceToken.Span.End >= classNode.CloseBraceToken.SpanStart)
        {
            return sourceText.GetSubText(originalSpan).ToString();
        }
        
        // For a class, we want the content between the opening and closing braces
        var bodySpan = TextSpan.FromBounds(
            classNode.OpenBraceToken.Span.End,
            classNode.CloseBraceToken.SpanStart);

        return sourceText.GetSubText(bodySpan).ToString();
    }

    private async Task<Dictionary<string, (Document document, TextSpan textSpan, SourceText sourceText)>>
        GetAllTypesAndMethodsInSolutionAsync(Solution solution)
    {
        var sw = Stopwatch.StartNew();
        var result = new Dictionary<string, (Document, TextSpan, SourceText)>();

        foreach (var project in solution.Projects)
        {
            _logger.LogDebug("Getting types and methods {project}", project.FilePath);
            if ((project.FilePath?.Contains("blog-projects") ?? false) == false)
            {
                _logger.LogDebug("Skipping {project}", project.FilePath);
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

    private static void ProcessTypeDeclarationsAsync(Document document, SyntaxNode rootSyntaxNode,
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

    private static void ProcessMethodDeclarationsAsync(Document document, SyntaxNode rootSyntaxNode,
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
            _workspace.Dispose();
        }

        _disposed = true;
    }

    ~DocumentProcessor()
    {
        Dispose(false);
    }
}