using Markdig;

namespace Thirty25.Web.BlogServices.Markdown;

/// <summary>
/// Extension method for adding tabbed code blocks to Markdig pipeline
/// </summary>
internal static class TabbedCodeBlocksExtensions
{
    public static MarkdownPipelineBuilder UseTabbedCodeBlocks(this MarkdownPipelineBuilder pipeline)
    {
        pipeline.Extensions.AddIfNotAlready(new TabbedCodeBlocksExtension());
        return pipeline;
    }
}