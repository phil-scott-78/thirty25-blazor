using BlazorStatic.Models;
using BlazorStatic.Services.Content.MarkdigExtensions.Navigation;
using Markdig;
using Markdig.Extensions.Yaml;
using Markdig.Renderers;
using Markdig.Syntax;
using Microsoft.Extensions.Logging;
using YamlDotNet.Serialization;

namespace BlazorStatic.Services.Content.MarkdigExtensions;

/// <summary>
/// Service for parsing and processing Markdown files with YAML front matter.
/// Provides caching, HTML conversion, and image path transformation capabilities.
/// </summary>
public class MarkdownParserService
{
    private readonly ILogger _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly BlazorStaticOptions _options;
    private readonly MarkdownPipeline _pipeline;

    /// <summary>
    /// Initializes a new instance of the <see cref = "MarkdownParserService"/> class.
    /// </summary>
    /// <param name = "logger">Logger for recording operational information</param>
    /// <param name = "serviceProvider">Service provider for dependency resolution</param>
    /// <param name = "options">Options for configuring the Markdown processing behavior</param>
    public MarkdownParserService(ILogger<MarkdownParserService> logger, IServiceProvider serviceProvider,
        BlazorStaticOptions options)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
        _options = options;

        _pipeline = options.MarkdownPipelineBuilder.Invoke(serviceProvider);
    }

    /// <summary>
    /// Parses a Markdown file, extracts the YAML front matter into a strongly typed object,
    /// and returns the markdown content (with front matter removed) and table of contents.
    /// </summary>
    /// <typeparam name="T">Type to deserialize the YAML front matter into. Must have a parameterless constructor.</typeparam>
    /// <param name="filePath">Path to the Markdown file to be processed.</param>
    /// <param name="contentPathRoot">The root of the content for this Markdown file.</param>
    /// <param name="pageUrlRoot">The url root for this Markdown file.</param>
    /// <param name="yamlDeserializer">Custom YAML deserializer instance. If null, the one from BlazorStaticOptions will be used.</param>
    /// <param name="preProcessFile">Optional function to preprocess the Markdown content before parsing.</param>
    /// <returns>
    /// Tuple containing:
    /// - The deserialized front matter of type T
    /// - The markdown content (with front matter removed)
    /// - The table of contents
    /// </returns>
internal async Task<(T frontMatter, string markdownContent, TocEntry[] tableOfContent)> ParseMarkdownFileAsync<T>(
    string filePath,
    string contentPathRoot,
    string pageUrlRoot,
    IDeserializer? yamlDeserializer = null,
    Func<IServiceProvider, string, string>? preProcessFile = null
) where T : IFrontMatter, new()
{
    // Check if the file exists
    if (!File.Exists(filePath))
    {
        _logger.LogWarning("File not found: {filePath}", filePath);
        return (new T(), string.Empty, []); // Use Array.Empty for clarity
    }

    yamlDeserializer ??= _options.FrontMatterDeserializer;
    var rawMarkdownContent = await File.ReadAllTextAsync(filePath); // Renamed to distinguish from processed

    var processedMarkdownContent = rawMarkdownContent;
    // Apply pre-processing if a preprocessor function was provided
    if (preProcessFile != null)
    {
        processedMarkdownContent = preProcessFile(_serviceProvider, processedMarkdownContent);
    }

    // Parse the Markdown content (the one that might have been pre-processed)
    var document = Markdown.Parse(processedMarkdownContent, _pipeline);

    // Extract the YAML front matter block and process it
    var yamlBlock = document.Descendants<YamlFrontMatterBlock>().FirstOrDefault();
    T frontMatter;
    string markdownWithoutFrontMatter;

    if (yamlBlock == null)
    {
        // No front matter found, create the default instance
        frontMatter = new T();
        markdownWithoutFrontMatter = processedMarkdownContent; // The whole content is markdown
    }
    else
    {
        // Extract the YAML content
        var frontMatterYaml = yamlBlock.Lines.ToString();

        try
        {
            // Deserialize the YAML content into the specified type
            frontMatter = yamlDeserializer.Deserialize<T>(frontMatterYaml);
        }
        catch (Exception e)
        {
            // Handle deserialization errors by using default values
            frontMatter = new T();
            _logger.LogWarning(
                "Cannot deserialize YAML front matter in {file}. The default one will be used! Error: {exceptionMessage}",
                filePath, e.Message + (e.InnerException != null ? " Inner: " + e.InnerException.Message : string.Empty)); // Improved error logging
        }

        // Get the Markdown content without the front matter using the original string
        // yamlBlock.Span.End gives the character index in 'processedMarkdownContent'
        // immediately *after* the YAML block.
        var contentStartIndex = yamlBlock.Span.End + 1;

        // We need to be careful: Span.End is the offset of the character *after* the block.
        // Typically, there's a newline after the closing '---' of the YAML block.
        // We want to get the content *after* this potential newline.
        // This will take the substring and then remove any leading whitespace
        // (like newlines that separated the YAML block from the content).
        markdownWithoutFrontMatter = contentStartIndex < processedMarkdownContent.Length
            ? processedMarkdownContent[contentStartIndex..].TrimStart()
            : string.Empty;

        // Remove the YAML block from the document object *before* generating TOC
        // This ensures TOC is generated only from the actual content.
        document.Remove(yamlBlock);
    }

    // Generate Table of Contents from the document *without* the front matter
    var toc = TocGenerator.GenerateTableOfContents(document);

    // Ensure the variable name in return matches the declared one (case sensitivity)
    return (frontMatter, markdownWithoutFrontMatter, toc);
}
    /// <summary>
    /// Renders markdown content to HTML using the configured pipeline and base URL.
    /// </summary>
    /// <param name="markdownContent">The markdown content to render.</param>
    /// <param name="baseUrl">The base URL for link rewriting.</param>
    /// <returns>The rendered HTML string.</returns>
    public string RenderMarkdownToHtml(string markdownContent, string baseUrl)
    {
        var document = Markdown.Parse(markdownContent, _pipeline);
        using var writer = new StringWriter();
        var htmlRenderer = new HtmlRenderer(writer)
        {
            LinkRewriter = s => LinkRewriter.RewriteUrl(s, baseUrl)
        };
        _pipeline.Setup(htmlRenderer);
        htmlRenderer.Render(document);
        writer.Flush();
        return writer.ToString();
    }

    /// <summary>
    /// Calculates the base URL for a Markdown file relative to its content root
    /// </summary>
    /// <param name="filePath">The absolute path of the Markdown file</param>
    /// <param name="contentPathRoot">The root content directory</param>
    /// <param name="pageUrlRoot">The base URL for pages</param>
    /// <returns>The base URL for the current file</returns>
    private string GetBaseUrl(string filePath, string contentPathRoot, string pageUrlRoot)
    {
        // Handle empty filepath edge-case
        if (string.IsNullOrEmpty(filePath))
            return pageUrlRoot;

        // Get the directory path and handle edge-cases
        var fileDirectory = Path.GetDirectoryName(filePath);
        if (string.IsNullOrEmpty(fileDirectory))
            return pageUrlRoot;

        // Normalize paths for consistent handling across platforms
        var normalizedContentRoot = NormalizePath(contentPathRoot);
        var normalizedFileDir = NormalizePath(fileDirectory);

        try
        {
            // Get the relative path from content root to file directory
            // This is more direct than the previous substring matching approach
            if (normalizedFileDir.StartsWith(normalizedContentRoot, StringComparison.OrdinalIgnoreCase))
            {
                // Simple case: file directory is within the content root
                string relativePath;

                if (normalizedFileDir.Length > normalizedContentRoot.Length)
                {
                    // Extract path after content root, accounting for trailing slash
                    int startPos = normalizedContentRoot.Length;
                    if (normalizedFileDir[startPos] == '/')
                        startPos++;

                    relativePath = normalizedFileDir[startPos..];
                }
                else
                {
                    // File is directly in the content root
                    relativePath = string.Empty;
                }

                return $"{pageUrlRoot.Trim('/')}/{relativePath.Trim('/')}";
            }

            // If we get here, the file isn't directly under the content root.
            // Try a more flexible approach for cases where paths don't directly match
            return GetBaseUrlUsingSegmentMatching(normalizedFileDir, normalizedContentRoot, pageUrlRoot);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error calculating base URL for {FilePath} with content root {ContentRoot}",
                filePath, contentPathRoot);
            return pageUrlRoot; // Fallback to page URL root in case of errors
        }
    }

    /// <summary>
    /// Fallback method to find the correct content paths using segment matching.
    /// Used when the file directory isn't a direct child of the content root.
    /// </summary>
    private string GetBaseUrlUsingSegmentMatching(string normalizedFileDir, string normalizedContentRoot,
        string pageUrlRoot)
    {
        var contentSegments = normalizedContentRoot.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var dirSegments = normalizedFileDir.Split('/', StringSplitOptions.RemoveEmptyEntries);

        // Find the common prefix and where the content path ends
        var contentEndIndex = FindContentPathEndIndex(contentSegments, dirSegments);

        if (contentEndIndex >= 0)
        {
            // Extract the path after the content root
            var relativePath = string.Join("/", dirSegments.Skip(contentEndIndex));
            return pageUrlRoot.Trim('/') + "/" + relativePath.Trim('/');
        }

        _logger.LogWarning("Could not locate content path '{ContentPathRoot}' in file directory '{FileDirectory}'",
            normalizedContentRoot, normalizedFileDir);
        return pageUrlRoot; // Fallback to page URL root
    }

    /// <summary>
    /// Finds the index where the content path ends in the directory segments
    /// </summary>
    private static int FindContentPathEndIndex(string[] contentSegments, string[] dirSegments)
    {
        if (contentSegments.Length == 0 || dirSegments.Length == 0)
            return 0;

        // Look for a complete match of content paths in the directory path
        for (var i = 0; i <= dirSegments.Length - contentSegments.Length; i++)
        {
            bool isMatch = true;

            // Check if all segments match at the current position
            for (var j = 0; j < contentSegments.Length; j++)
            {
                if (!string.Equals(dirSegments[i + j], contentSegments[j], StringComparison.OrdinalIgnoreCase))
                {
                    isMatch = false;
                    break;
                }
            }

            if (isMatch)
                return i + contentSegments.Length;
        }

        return -1; // No match found
    }


    private static string NormalizePath(string path)
    {
        if (string.IsNullOrEmpty(path))
        {
            return path;
        }

        // Replace backslashes with forward slashes for web compatibility
        path = path.Replace('\\', '/');

        // Trim trailing slashes
        return path.TrimEnd('/');
    }
}