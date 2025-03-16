using System.Text.RegularExpressions;
using Markdig;
using Markdig.Extensions.Yaml;
using Markdig.Syntax;
using Microsoft.Extensions.Logging;
using YamlDotNet.Serialization;

namespace BlazorStatic.Services;

/// <summary>
/// 
/// </summary>
/// <param name="options"></param>
/// <param name="logger"></param>
public class MarkdownService(BlazorStaticOptions options, ILogger<MarkdownService> logger)
{
    /// <summary>
    ///     Parses a markdown file and returns the HTML content.
    ///     Uses the options.MarkdownPipeline to parse the markdown (set this in BlazorStaticOptions).
    /// </summary>
    /// <param name="filePath"></param>
    /// <param name="mediaPaths">
    ///     If you need to change media paths of images, do it here.
    ///     Used in internal parsing method. Translating "media/img.jpg" to "path/configured/by/useStaticFiles/img.jpg"
    /// </param>
    /// <returns></returns>
    public async Task<string> ParseMarkdownFile(string filePath,
        (string mediaPathToBeReplaced, string mediaPathNew)? mediaPaths = null)
    {
        var markdownContent = await File.ReadAllTextAsync(filePath);
        var htmlContent = Markdown.ToHtml(ReplaceImagePathsInMarkdown(markdownContent, mediaPaths),
            options.MarkdownPipeline);
        return htmlContent;
    }

    /// <summary>
    ///     Parses a markdown file and returns the HTML content and the front matter.
    /// </summary>
    /// <param name="filePath"></param>
    /// <param name="mediaPaths"></param>
    /// <param name="yamlDeserializer"></param>
    /// <param name="preProcessFile"></param>
    /// <typeparam name="T"></typeparam>
    /// <returns></returns>
    public (T frontMattestring, string htmlContent) ParseMarkdownFile<T>(string filePath,
        (string mediaPathToBeReplaced, string mediaPathNew)? mediaPaths = null,
        IDeserializer? yamlDeserializer = null, Func<string, string>? preProcessFile = null) where T : new()
    {
        yamlDeserializer ??= options.FrontMatterDeserializer;
        var markdownContent = File.ReadAllText(filePath);
        if (preProcessFile != null)
        {
            markdownContent = preProcessFile(markdownContent);
        }

        var document = Markdown.Parse(markdownContent, options.MarkdownPipeline);

        var yamlBlock = document.Descendants<YamlFrontMatterBlock>().FirstOrDefault();
        T frontMatter;
        if (yamlBlock == null)
        {
            //logger.LogWarning("No YAML front matter found in {file}. The default one will be used!", file);
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

    //replace the image paths in the markdown content with the correct relative path
    //e.g.: media/img1.jpg => Content/Blog/media/img1.jpg
    //Look for BlazorStaticContentOptions.MediaFolderRelativeToContentPath, MediaFolderRelativeToContentPath and ContentPath
    //this way the .md file can be edited with images in folder next to them, like users are used to.
    private static string ReplaceImagePathsInMarkdown(string markdownContent,
        (string originalPath, string newPath)? mediaPaths = null)
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