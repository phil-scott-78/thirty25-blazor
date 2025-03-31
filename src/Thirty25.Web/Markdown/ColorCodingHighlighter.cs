using System.Text;
using Markdig;
using Markdig.Parsers;
using Markdig.Renderers;
using Markdig.Renderers.Html;
using Markdig.Syntax;
using Thirty25.Web.BlogServices;

namespace Thirty25.Web.Markdown
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
                new CodeHightRenderer(
                    codeBlockRenderer,
                    roslynHighlighter)
            );
        }
    }

    internal sealed class CodeHightRenderer(
        CodeBlockRenderer codeBlockRenderer,
        RoslynHighlighterService roslynHighlighter) : HtmlObjectRenderer<CodeBlock>
    {
        protected override void Write(HtmlRenderer renderer, CodeBlock codeBlock)
        {
            if (codeBlock is not FencedCodeBlock fencedCodeBlock ||
                codeBlock.Parser is not FencedCodeBlockParser fencedCodeBlockParser)
            {
                codeBlockRenderer.Write(renderer, codeBlock);
                return;
            }

            var languageId = fencedCodeBlock.Info!.Replace(fencedCodeBlockParser.InfoPrefix!, string.Empty);
            if (!string.IsNullOrWhiteSpace(languageId))
            {
                var code = ExtractCode(codeBlock);

                if (languageId is "csharp" or "c#" or "cs")
                {
                    var html = roslynHighlighter.Highlight(code, Language.CSharp);
                    renderer.Write(html);
                    return;
                }
                
                if (languageId is "vb" or "vbnet")
                {
                    var html = roslynHighlighter.Highlight(code, Language.VisualBasic);
                    renderer.Write(html);
                    return;
                }
            }

            codeBlockRenderer.Write(renderer, codeBlock);
        }

        private static string ExtractCode(LeafBlock leafBlock)
        {
            var code = new StringBuilder();

            // ReSharper disable once NullCoalescingConditionIsAlwaysNotNullAccordingToAPIContract
            var lines = leafBlock.Lines.Lines ?? [];
            var totalLines = lines.Length;

            for (var index = 0; index < totalLines; index++)
            {
                var line = lines[index];
                var slice = line.Slice;

                if (slice.Text == null)
                {
                    continue;
                }

                var lineText = slice.Text.Substring(slice.Start, slice.Length);

                if (index > 0)
                {
                    code.AppendLine();
                }

                code.Append(lineText);
            }

            return code.ToString();
        }
    }

    internal static class MarkdownPipelineBuilderExtensions
    {
        /// <summary>
        ///     Use Roslyn and ColorCode to colorize HTML generated from Markdown.
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
}