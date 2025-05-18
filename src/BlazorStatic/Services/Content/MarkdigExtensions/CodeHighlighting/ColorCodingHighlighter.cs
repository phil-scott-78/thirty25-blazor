using BlazorStatic.Services.Content.Roslyn;
using Markdig;
using Markdig.Renderers;
using Markdig.Renderers.Html;

namespace BlazorStatic.Services.Content.MarkdigExtensions.CodeHighlighting
{
    internal class ColorCodingHighlighter(
        RoslynHighlighterService roslynHighlighter,
        CodeHighlightRenderOptions? options = null) : IMarkdownExtension
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
                new CodeHighlightRenderer(roslynHighlighter, options)
            );
        }
    }
}