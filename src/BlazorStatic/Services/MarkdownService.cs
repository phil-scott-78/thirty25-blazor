﻿using System.Collections.Concurrent;
using System.Text;
using System.Text.RegularExpressions;
using BlazorStatic.Models;
using Markdig;
using Markdig.Extensions.Yaml;
using Markdig.Renderers.Html;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using Microsoft.Extensions.Logging;
using YamlDotNet.Serialization;

namespace BlazorStatic.Services;

/// <summary>
/// Service for parsing and processing Markdown files with YAML front matter.
/// Provides caching, HTML conversion, and image path transformation capabilities.
/// </summary>
public partial class MarkdownService
{
    private readonly ILogger _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly BlazorStaticOptions _options;
    private readonly MarkdownPipeline _pipeline;

    /// <summary>
    /// Initializes a new instance of the <see cref = "MarkdownService"/> class.
    /// </summary>
    /// <param name = "logger">Logger for recording operational information</param>
    /// <param name = "serviceProvider">Service provider for dependency resolution</param>
    /// <param name = "options">Options for configuring the markdown processing behavior</param>
    public MarkdownService(ILogger<MarkdownService> logger, IServiceProvider serviceProvider,
        BlazorStaticOptions options)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
        _options = options;

        _pipeline = options.MarkdownPipelineBuilder.Invoke(serviceProvider);
    }

    /// <summary>
    /// Parses a Markdown file, extracts the YAML front matter into a strongly-typed object,
    /// and converts the remaining content to HTML. Uses caching for improved performance.
    /// </summary>
    /// <typeparam name="T">Type to deserialize the YAML front matter into. Must have a parameterless constructor.</typeparam>
    /// <param name="filePath">Path to the Markdown file to be processed.</param>
    /// <param name="contentPathRoot">The root of the content for this markdown file.</param>
    /// <param name="pageUrlRoot">The url root for this Markdown file.</param>
    /// <param name="yamlDeserializer">
    /// Custom YAML deserializer instance. If null, the one from BlazorStaticOptions will be used.
    /// </param>
    /// <param name="preProcessFile">
    /// Optional function to preprocess the Markdown content before parsing.
    /// Takes the service provider and raw file content as inputs and returns modified content.
    /// </param>
    /// <param name="postProcessHtml">
    /// Optional function to postProcess the Markdown content before parsing.
    /// Takes the service provider, frotnMatter and HTML content as inputs and returns modified content.
    /// </param>
    /// <returns>
    /// Tuple containing:
    /// - The deserialized front matter of type T
    /// - The HTML content generated from the Markdown (without the front matter)
    /// </returns>
    public async Task<(T frontMatter, string htmlContent, TocEntry[] tableOfContent)> ParseMarkdownFileAsync<T>(
        string filePath,
        string contentPathRoot,
        string pageUrlRoot,
        IDeserializer? yamlDeserializer = null,
        Func<IServiceProvider, string, string>? preProcessFile = null,
        Func<IServiceProvider, T, string, (T, string)>? postProcessHtml = null
    ) where T : IFrontMatter, new()
    {
        // Check if file exists
        if (!File.Exists(filePath))
        {
            _logger.LogWarning("File not found: {filePath}", filePath);
            return (new T(), string.Empty, []);
        }

        // Get file's last write time
        var fileLastModified = File.GetLastWriteTime(filePath);

        // Create a cache key that includes the file path, path root and page url root.
        // changes to those *should* wipe the cache regardless, but just in case
        var cacheKey = $"{filePath}_{contentPathRoot}_{pageUrlRoot}";

        // If not in cache or cache is invalid, process the file
        yamlDeserializer ??= _options.FrontMatterDeserializer;

        // Read the file content
        var markdownContent = await File.ReadAllTextAsync(filePath);

        // Apply pre-processing if a preprocessor function was provided
        if (preProcessFile != null)
        {
            markdownContent = preProcessFile(_serviceProvider, markdownContent);
        }

        // Parse the markdown content
        var document = Markdown.Parse(markdownContent, _pipeline);

        // Extract the YAML front matter block, if present
        var yamlBlock = document.Descendants<YamlFrontMatterBlock>().FirstOrDefault();
        T frontMatter;
        if (yamlBlock == null)
        {
            // No front matter found, create default instance
            frontMatter = new T();
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
                    filePath, e.Message + e.InnerException?.Message);
            }
        }

        // Extract content without front matter
        var contentWithoutFrontMatter = markdownContent[(yamlBlock == null ? 0 : yamlBlock.Span.End + 1)..];

        // Replace image paths if needed and convert to HTML
        var replacedMarkdown =
            ReplaceImagePathsInMarkdown(contentWithoutFrontMatter, filePath, contentPathRoot, pageUrlRoot);

        var parsedMarkdown = Markdown.Parse(replacedMarkdown, _pipeline);
        var toc = GenerateTableOfContents(parsedMarkdown);
        var htmlContent = parsedMarkdown.ToHtml(_pipeline);

        if (postProcessHtml != null)
        {
            (frontMatter, htmlContent) = postProcessHtml.Invoke(_serviceProvider, frontMatter, htmlContent);
        }

        _logger.LogDebug("Added/updated cache entry for {filePath}", filePath);

        return (frontMatter, htmlContent, toc);
    }


    /// <summary>
    /// Replaces relative image and link paths in markdown content with absolute paths.
    /// </summary>
    /// <param name="markdownContent">The markdown content to process.</param>
    /// <param name="filePath">The file path of the markdown file being processed.</param>
    /// <param name="contentPathRoot">The root path of content files.</param>
    /// <param name="pageUrlRoot">The root URL for the web pages.</param>
    /// <returns>The markdown content with replaced paths.</returns>
    private string ReplaceImagePathsInMarkdown(string markdownContent, string filePath, string contentPathRoot,
        string pageUrlRoot)
    {
        if (string.IsNullOrEmpty(markdownContent)) return markdownContent;

        // Get the directory of the current markdown file relative to the content root
        var fileDirectory = Path.GetDirectoryName(filePath);
        if (string.IsNullOrEmpty(fileDirectory))
        {
            return markdownContent;
        }

        // Normalize paths to ensure consistent handling across platforms
        contentPathRoot = NormalizePath(contentPathRoot);
        fileDirectory = NormalizePath(fileDirectory);

        // Calculate the relative path from content root to file directory
        string relativePath;
        if (fileDirectory.StartsWith(contentPathRoot, StringComparison.OrdinalIgnoreCase))
        {
            relativePath = fileDirectory.Substring(contentPathRoot.Length)
                .TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }
        else
        {
            _logger.LogWarning("File directory {FileDirectory} is not within content path root {ContentPathRoot}",
                fileDirectory, contentPathRoot);
            relativePath = string.Empty;
        }

        // Combine the page URL root with the relative path to get the base URL for the current file
        var baseUrl = CombineUrl(pageUrlRoot, relativePath);


        // Replace image paths
        markdownContent = ImageRegex().Replace(markdownContent, match =>
        {
            var altText = match.Groups[1].Value;
            var imagePath = match.Groups[2].Value;

            // Replace the path if it's not already absolute
            var newPath = GetAbsolutePath(imagePath, baseUrl);

            return $"![{altText}]({newPath})";
        });

        // Replace link paths
        markdownContent = LinkRegex().Replace(markdownContent, match =>
        {
            var linkText = match.Groups[1].Value;
            var linkPath = match.Groups[2].Value;

            // Skip links that are not file paths (e.g., URLs, anchors)
            if (IsExternalUrl(linkPath) || IsAnchorLink(linkPath) || ContainsQueryOrFragment(linkPath))
            {
                return match.Value;
            }

            // Replace the path if it's not already absolute
            var newPath = GetAbsolutePath(linkPath, baseUrl);

            return $"[{linkText}]({newPath})";
        });

        return markdownContent;
    }

    private static bool IsExternalUrl(string path) =>
        path.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
        path.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ||
        path.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase) ||
        path.StartsWith("tel:", StringComparison.OrdinalIgnoreCase) ||
        path.StartsWith("ftp:", StringComparison.OrdinalIgnoreCase);

    private static bool IsAnchorLink(string path) => path.StartsWith('#');

    private static bool ContainsQueryOrFragment(string path) =>
        !IsExternalUrl(path) && (path.Contains('?') || path.Contains('#'));

    /// <summary>
    /// Normalizes a file path by replacing backslashes with forward slashes and trimming trailing slashes.
    /// </summary>
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

    private static string CombineUrl(params string[] segments)
    {
        if (segments.Length == 0)
        {
            return string.Empty;
        }

        var normalizedSegments = segments
            .Where(s => !string.IsNullOrEmpty(s))
            .Select(s => s.Trim('/'))
            .ToArray();

        if (normalizedSegments.Length == 0)
        {
            return "/";
        }

        return string.Join("/", normalizedSegments);
    }

    private static string GetAbsolutePath(string relativePath, string baseUrl)
    {
        // If the path is already absolute, return it as is
        if (relativePath.StartsWith('/'))
        {
            return relativePath;
        }

        // Handle relative paths that start with ../
        if (!relativePath.StartsWith("../"))
        {
            // Regular relative path, just combine with base URL
            return CombineUrl(baseUrl, relativePath);
        }

        var baseSegments = baseUrl.Split('/')
            .Where(s => !string.IsNullOrEmpty(s))
            .ToList();

        var relativeSegments = relativePath.Split('/').ToList();

        // Count how many levels to go up
        var levelsUp = 0;
        while (relativeSegments.Count > 0 && relativeSegments[0] == "..")
        {
            levelsUp++;
            relativeSegments.RemoveAt(0);
        }

        // Remove the appropriate number of segments from the base URL
        baseSegments = baseSegments.Take(Math.Max(0, baseSegments.Count - levelsUp)).ToList();

        // Combine the remaining base segments with the relative segments
        var resultSegments = baseSegments.Concat(relativeSegments);
        return string.Join("/", resultSegments);
    }

    private static TocEntry[] GenerateTableOfContents(MarkdownDocument document)
    {
        var tocEntries = new List<TocEntry>();
        var headerStack = new Stack<(TocEntry Entry, int Level)>();

        // Traverse the document to find headings
        foreach (var node in document.Descendants())
        {
            if (node is not HeadingBlock headingBlock) continue;
            var level = headingBlock.Level;

            if (headingBlock.Inline == null)
            {
                continue;
            }

            // Extract title from the heading
            var title = GetPlainTextFromInline(headingBlock.Inline);

            // Get the ID that will be used in the HTML output
            var id = headingBlock.TryGetAttributes()?.Id;

            // Skip headers without IDs
            if (id == null)
            {
                continue;
            }

            var newEntry = new TocEntry(title, id, []);

            // Pop entries from stack that are at the same or higher level
            while (headerStack.Count > 0 && headerStack.Peek().Level >= level)
            {
                headerStack.Pop();
            }

            if (headerStack.Count == 0)
            {
                // This is a top-level heading
                tocEntries.Add(newEntry);
            }
            else
            {
                // Add as child to parent heading
                var (parentEntry, parentLevel) = headerStack.Peek(); // Store the parent level here
                var parentChildren = parentEntry.Children.ToList();
                parentChildren.Add(newEntry);

                // Create updated parent with new children
                var updatedParent = parentEntry with { Children = parentChildren.ToArray() };

                // Pop the old parent and push the updated one with the same level
                headerStack.Pop();
                headerStack.Push((updatedParent, parentLevel)); // Use the stored parent level

                // Update in the main list if it's a top-level entry
                if (headerStack.Count == 1)
                {
                    int index = tocEntries.IndexOf(parentEntry);
                    if (index >= 0) // Make sure we found the parent
                    {
                        tocEntries[index] = updatedParent;
                    }
                }
            }

            headerStack.Push((newEntry, level));
        }

        return tocEntries.ToArray();
    }

    private static string GetPlainTextFromInline(ContainerInline container)
    {
        var sb = new StringBuilder();

        foreach (var inline in container)
        {
            switch (inline)
            {
                case LiteralInline literal:
                    sb.Append(literal.Content.ToString());
                    break;

                case EmphasisInline emphasis:
                    sb.Append(GetPlainTextFromInline(emphasis));
                    break;

                case CodeInline code:
                    sb.Append(code.Content);
                    break;

                case ContainerInline nestedContainer:
                    sb.Append(GetPlainTextFromInline(nestedContainer));
                    break;

                // Add more cases as needed for other inline types

                default:
                    // For any other inline type, try to get content if it's a ContainerInline
                    if (inline is ContainerInline otherContainer)
                    {
                        sb.Append(GetPlainTextFromInline(otherContainer));
                    }

                    break;
            }
        }

        return sb.ToString();
    }

    [GeneratedRegex(@"!\[([^\]]*)\]\(([^)]+)\)")]
    private static partial Regex ImageRegex();

    [GeneratedRegex(@"(?<!!)\[([^\]]*)\]\(([^)]+)\)")]
    private static partial Regex LinkRegex();
}