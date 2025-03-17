using System.Text.RegularExpressions;
using Markdig;
using Markdig.Extensions.Yaml;
using Markdig.Syntax;
using Microsoft.Extensions.Logging;
using YamlDotNet.Serialization;

namespace BlazorStatic.Services;

/// <summary>
/// Service for processing Markdown files, including parsing content, extracting front matter,
/// and converting Markdown to HTML for use in Blazor Static sites.
/// </summary>
/// <param name="options">Configuration options for BlazorStatic, including Markdown pipeline settings.</param>
/// <param name="logger">Logger instance for recording warnings and errors during Markdown processing.</param>
/// <param name="serviceProvider">Service provider for resolving dependencies needed during preprocessing.</param>
public class MarkdownService(BlazorStaticOptions options, ILogger<MarkdownService> logger, IServiceProvider serviceProvider)
{
    /// <summary>
    /// Parses a Markdown file and converts it to HTML.
    /// </summary>
    /// <param name="filePath">Path to the Markdown file to be processed.</param>
    /// <param name="mediaPaths">
    /// Optional tuple containing paths for image reference translation:
    /// - Item1: Original media path prefix to be replaced (e.g., "media")
    /// - Item2: New media path prefix (e.g., "path/configured/by/useStaticFiles")
    /// This enables proper resolution of image references when content is served from a different location.
    /// </param>
    /// <returns>HTML content generated from the Markdown file.</returns>
    public async Task<string> ParseMarkdownFile(string filePath,
        (string mediaPathToBeReplaced, string mediaPathNew)? mediaPaths = null)
    {
        var markdownContent = await File.ReadAllTextAsync(filePath);
        var htmlContent = Markdown.ToHtml(ReplaceImagePathsInMarkdown(markdownContent, mediaPaths),
            options.MarkdownPipeline);
        return htmlContent;
    }

    /// <summary>
    /// Parses a Markdown file, extracts the YAML front matter into a strongly-typed object,
    /// and converts the remaining content to HTML.
    /// </summary>
    /// <typeparam name="T">Type to deserialize the YAML front matter into. Must have a parameterless constructor.</typeparam>
    /// <param name="filePath">Path to the Markdown file to be processed.</param>
    /// <param name="mediaPaths">
    /// Optional tuple for image path translation:
    /// - Item1: Original media path prefix to be replaced
    /// - Item2: New media path prefix
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
        (string mediaPathToBeReplaced, string mediaPathNew)? mediaPaths = null,
        IDeserializer? yamlDeserializer = null, Func<IServiceProvider, string, string>? preProcessFile = null) where T : new()
    {
        yamlDeserializer ??= options.FrontMatterDeserializer;
        var markdownContent = File.ReadAllText(filePath);
        if (preProcessFile != null)
        {
            markdownContent = preProcessFile(serviceProvider, markdownContent);
        }

        var document = Markdown.Parse(markdownContent, options.MarkdownPipeline);

        var yamlBlock = document.Descendants<YamlFrontMatterBlock>().FirstOrDefault();
        T frontMatter;
        if (yamlBlock == null)
        {
            // No front matter found, create default instance
            frontMatter = new T();
        }
        else
        {
            var frontMatterYaml = yamlBlock.Lines.ToString();

            try
            {
                frontMatter = yamlDeserializer.Deserialize<T>(frontMatterYaml);
            }
            catch (Exception e)
            {
                frontMatter = new T();
                logger.LogWarning(
                    "Cannot deserialize YAML front matter in {file}. The default one will be used! Error: {exceptionMessage}",
                    filePath, e.Message + e.InnerException?.Message);
            }
        }

        var contentWithoutFrontMatter = markdownContent[(yamlBlock == null ? 0 : yamlBlock.Span.End + 1)..];
        var htmlContent = Markdown.ToHtml(ReplaceImagePathsInMarkdown(contentWithoutFrontMatter, mediaPaths), options.MarkdownPipeline);
        return (frontMatter, htmlContent);
    }

    /// <summary>
    /// Replaces media paths in Markdown content to ensure proper image resolution when content is served.
    /// Handles both Markdown image syntax ![alt](path) and HTML img tags.
    /// </summary>
    /// <param name="markdownContent">The raw Markdown content to process.</param>
    /// <param name="mediaPaths">
    /// Optional tuple containing:
    /// - originalPath: Path prefix to be replaced (e.g., "media")
    /// - newPath: New path prefix to use instead (e.g., "Content/Blog/media")
    /// </param>
    /// <returns>Markdown content with updated image references.</returns>
    /// <remarks>
    /// This allows content authors to use relative paths in their Markdown files
    /// (e.g., "media/img.jpg") while ensuring proper resolution when the content
    /// is served from a different location in the site structure.
    /// </remarks>
    private static string ReplaceImagePathsInMarkdown(string markdownContent, (string originalPath, string newPath)? mediaPaths = null)
    {
        if (mediaPaths == null)
        {
            return markdownContent;
        }

        // Pattern for Markdown image syntax: ![alt text](path)
        var markdownPattern = $@"!\[(.*?)\]\({mediaPaths.Value.originalPath}\/(.*?)\)";
        var markdownReplacement = $"![$1]({mediaPaths.Value.newPath}/$2)";

        // Pattern for HTML img tag: <img src="path" .../>
        var htmlPattern = $"""
                           <img\s+[^>]*src\s*=\s*"{mediaPaths.Value.originalPath}/(.*?)"
                           """;

        var htmlReplacement = $"<img src=\"{mediaPaths.Value.newPath}/$1\"";

        // First, replace the Markdown-style image paths
        var modifiedMarkdown = Regex.Replace(markdownContent, markdownPattern, markdownReplacement);

        // Then, replace the HTML-style image paths
        modifiedMarkdown = Regex.Replace(modifiedMarkdown, htmlPattern, htmlReplacement);

        return modifiedMarkdown;
    }
}