using System.Text;
using Markdig.Parsers;
using Markdig.Renderers;
using Markdig.Renderers.Html;
using Markdig.Syntax;

namespace Thirty25.Web.BlogServices.Markdown;

internal sealed class CodeHighlightRenderer(
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

            switch (languageId)
            {
                case "csharp" or "c#" or "cs":
                {
                    var html = roslynHighlighter.Highlight(code, Language.CSharp);
                    renderer.Write(html);
                    return;
                }
                case "csharp:xmldocid,bodyonly":
                {
                    var html = roslynHighlighter.HighlightExample(code, true);
                    renderer.Write(html);
                    return;
                }
                case "csharp:xmldocid":
                {
                    var html = roslynHighlighter.HighlightExample(code, false);
                    renderer.Write(html);
                    return;
                }
                case "vb" or "vbnet":
                {
                    var html = roslynHighlighter.Highlight(code, Language.VisualBasic);
                    renderer.Write(html);
                    return;
                }
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