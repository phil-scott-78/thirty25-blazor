using Markdig;

namespace Thirty25.Web.BlogServices.Markdown;

internal static class MarkdownPipelineBuilderExtensions
{
    /// <summary>
    ///     Use Roslyn to colorize HTML generated from Markdown.
    /// </summary>
    /// <returns>The <see cref="MarkdownPipelineBuilder"/> configured with ColorCode.</returns>
    public static MarkdownPipelineBuilder UseSyntaxHighlighting(
        this MarkdownPipelineBuilder markdownPipelineBuilder,
        RoslynHighlighterService roslynHighlighter)
    {
        markdownPipelineBuilder.Extensions.Add(new ColorCodingHighlighter(roslynHighlighter));

        return markdownPipelineBuilder;
    }
}