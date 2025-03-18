using System.Collections.Concurrent;
using System.Reflection.Metadata;
using System.Text.RegularExpressions;
using BlazorStatic.Models;
using BlazorStatic.Services;
using Markdig;
using Markdig.Extensions.Yaml;
using Markdig.Syntax;
using Microsoft.Extensions.Logging;
using YamlDotNet.Serialization;

namespace BlazorStatic.Services;

/// <summary>
/// Record to represent a media path transformation for images in markdown content
/// </summary>
/// <param name="MediaPathToBeReplaced">The original path prefix to be replaced</param>
/// <param name="MediaPathNew">The new path prefix to use instead</param>
public record MediaPath(string MediaPathToBeReplaced, string MediaPathNew);

/// <summary>
/// Service for parsing and processing Markdown files with YAML front matter.
/// Provides caching, HTML conversion, and image path transformation capabilities.
/// </summary>
public class MarkdownService
{
    private readonly ILogger _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly BlazorStaticOptions _options;
    
    // Cache to store processed markdown files
    private static readonly ConcurrentDictionary<string, CachedMarkdownEntry> MarkdownCache = new();

    /// <summary>
    /// Clears the markdown cache when metadata is updated.
    /// Used by the MetadataUpdateHandler to refresh content after changes.
    /// </summary>
    private static void ClearCache()
    {
        MarkdownCache.Clear();
    }

    /// <summary>
    /// Private class to store cached markdown parsing results
    /// </summary>
    private record CachedMarkdownEntry(
        DateTime LastModified,
        IFrontMatter FrontMatter,
        string HtmlContent,
        MediaPath? MediaPath);

    /// <summary>
    /// Initializes a new instance of the <see cref="MarkdownService"/> class.
    /// </summary>
    /// <param name="logger">Logger for recording operational information</param>
    /// <param name="serviceProvider">Service provider for dependency resolution</param>
    /// <param name="options">Options for configuring the markdown processing behavior</param>
    public MarkdownService(ILogger<MarkdownService> logger, IServiceProvider serviceProvider, BlazorStaticOptions options)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
        _options = options;
        
        HotReloadManager.Subscribe(ClearCache);
    }

    /// <summary>
    /// Parses a Markdown file, extracts the YAML front matter into a strongly-typed object,
    /// and converts the remaining content to HTML. Uses caching for improved performance.
    /// </summary>
    /// <typeparam name="T">Type to deserialize the YAML front matter into. Must have a parameterless constructor.</typeparam>
    /// <param name="filePath">Path to the Markdown file to be processed.</param>
    /// <param name="mediaPaths">
    /// Optional media path transformations for image references:
    /// - MediaPathToBeReplaced: Original media path prefix to be replaced
    /// - MediaPathNew: New media path prefix to use instead
    /// </param>
    /// <param name="yamlDeserializer">
    /// Custom YAML deserializer instance. If null, the one from BlazorStaticOptions will be used.
    /// </param>
    /// <param name="preProcessFile">
    /// Optional function to preprocess the Markdown content before parsing.
    /// Takes the service provider and raw file content as inputs and returns modified content.
    /// </param>
    /// <returns>
    /// Tuple containing:
    /// - The deserialized front matter of type T
    /// - The HTML content generated from the Markdown (without the front matter)
    /// </returns>
    public (T frontMatter, string htmlContent) ParseMarkdownFile<T>(string filePath,
        MediaPath? mediaPaths = null,
        IDeserializer? yamlDeserializer = null, Func<IServiceProvider, string, string>? preProcessFile = null) where T : IFrontMatter, new()
    {
        // Check if file exists
        if (!File.Exists(filePath))
        {
            _logger.LogWarning("File not found: {filePath}", filePath);
            return (new T(), string.Empty);
        }

        // Get file's last write time
        var fileLastModified = File.GetLastWriteTime(filePath);
        
        // Create a cache key that includes the file path and media paths
        var cacheKey = $"{filePath}_{mediaPaths?.MediaPathToBeReplaced ?? "null"}_{mediaPaths?.MediaPathNew ?? "null"}";
        
        // Check if the file is in the cache and is still valid
        if (MarkdownCache.TryGetValue(cacheKey, out var cachedEntry))
        {
            // If the cached version is newer than or equal to the file's last modified time
            // and the media paths match, return the cached version
            if (cachedEntry.LastModified >= fileLastModified && 
                AreMediaPathsEqual(cachedEntry.MediaPath, mediaPaths))
            {
                _logger.LogDebug("Using cached version of {filePath}", filePath);
                return ((T)cachedEntry.FrontMatter, cachedEntry.HtmlContent);
            }
        }
        
        // If not in cache or cache is invalid, process the file
        yamlDeserializer ??= _options.FrontMatterDeserializer;
        
        // Read the file content
        var markdownContent = File.ReadAllText(filePath);
        
        // Apply pre-processing if a preprocessor function was provided
        if (preProcessFile != null)
        {
            markdownContent = preProcessFile(_serviceProvider, markdownContent);
        }

        // Parse the markdown content
        var document = Markdown.Parse(markdownContent, _options.MarkdownPipeline);

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
        var htmlContent = Markdown.ToHtml(ReplaceImagePathsInMarkdown(contentWithoutFrontMatter, mediaPaths), _options.MarkdownPipeline);
        
        // Store the result in the cache
        MarkdownCache[cacheKey] = new CachedMarkdownEntry(fileLastModified, frontMatter, htmlContent, mediaPaths);
        
        _logger.LogDebug("Added/updated cache entry for {filePath}", filePath);
        
        return (frontMatter, htmlContent);
    }

    /// <summary>
    /// Compares two media path objects for equality
    /// </summary>
    /// <param name="path1">First media path object</param>
    /// <param name="path2">Second media path object</param>
    /// <returns>True if both paths are equal or both are null, false otherwise</returns>
    private static bool AreMediaPathsEqual(MediaPath? path1, MediaPath? path2)
    {
        if (path1 == null && path2 == null)
            return true;
            
        if (path1 == null || path2 == null)
            return false;
            
        return path1.Equals(path2);
    }

    /// <summary>
    /// Replaces media paths in Markdown content to ensure proper image resolution when content is served.
    /// Handles both Markdown image syntax ![alt](path) and HTML img tags.
    /// </summary>
    /// <param name="markdownContent">The raw Markdown content to process.</param>
    /// <param name="mediaPaths">
    /// Optional media path transformation containing:
    /// - MediaPathToBeReplaced: Path prefix to be replaced (e.g., "media")
    /// - MediaPathNew: New path prefix to use instead (e.g., "Content/Blog/media")
    /// </param>
    /// <returns>Markdown content with updated image references.</returns>
    /// <remarks>
    /// This allows content authors to use relative paths in their Markdown files
    /// (e.g., "media/img.jpg") while ensuring proper resolution when the content
    /// is served from a different location in the site structure.
    /// </remarks>
    private static string ReplaceImagePathsInMarkdown(string markdownContent, MediaPath? mediaPaths = null)
    {
        // If no media path transformation is specified, return the content unchanged
        if (mediaPaths == null)
        {
            return markdownContent;
        }

        // Pattern for Markdown image syntax: ![alt text](path)
        var markdownPattern = $"""
                               !\[(.*?)\]\({mediaPaths.MediaPathToBeReplaced}\/(.*?)\)
                               """;
        var markdownReplacement = $"![$1]({mediaPaths.MediaPathNew}/$2)";

        // Pattern for HTML img tag: <img src="path" .../>
        var htmlPattern = $"""
                           <img\s+[^>]*src\s*=\s*"{mediaPaths.MediaPathToBeReplaced}/(.*?)"
                           """;

        var htmlReplacement = $"""
                               <img src="{mediaPaths.MediaPathNew}/$1"
                               """;

        // First, replace the Markdown-style image paths
        var modifiedMarkdown = Regex.Replace(markdownContent, markdownPattern, markdownReplacement);

        // Then, replace the HTML-style image paths
        modifiedMarkdown = Regex.Replace(modifiedMarkdown, htmlPattern, htmlReplacement);

        return modifiedMarkdown;
    }
}