using Markdig;
using Markdig.Renderers;
using Markdig.Renderers.Html;
using Thirty25.Web.BlogServices.Roslyn;

namespace Thirty25.Web.BlogServices.Markdown
{
    internal class ColorCodingHighlighter(RoslynHighlighterService roslynHighlighter) : IMarkdownExtension
    {
        public void Setup(MarkdownPipelineBuilder pipeline)
        {
        }

        public void Setup(MarkdownPipeline pipeline, IMarkdownRenderer renderer)
        {
            if (renderer is not TextRendererBase<HtmlRenderer> htmlRenderer)
            {
                return;
            }

            var codeBlockRenderer = htmlRenderer.ObjectRenderers.FindExact<CodeBlockRenderer>();

            if (codeBlockRenderer is not null)
            {
                htmlRenderer.ObjectRenderers.Remove(codeBlockRenderer);
            }
            else
            {
                codeBlockRenderer = new CodeBlockRenderer();
            }

            htmlRenderer.ObjectRenderers.AddIfNotAlready(
                new CodeHighlightRenderer(
                    codeBlockRenderer,
                    roslynHighlighter)
            );
        }
    }
}