using System.Text;
using Markdig.Parsers;
using Markdig.Renderers;
using Markdig.Renderers.Html;
using Markdig.Syntax;
using Thirty25.Web.BlogServices.Roslyn;

namespace Thirty25.Web.BlogServices.Markdown;

internal sealed class CodeHighlightRenderer(
    CodeBlockRenderer codeBlockRenderer,
    RoslynHighlighterService roslynHighlighter) : HtmlObjectRenderer<CodeBlock>
{
protected override void Write(HtmlRenderer renderer, CodeBlock codeBlock)
{
    var preCss = "overflow-x-auto scheme-dark font-mono text-xs md:text-sm font-light leading-relaxed w-full";
    var containerCss = "";
    if (codeBlock.Parent is not TabbedCodeBlock)
    {
        // if we aren't in a tab block, then let's create ourselves a container
        containerCss = "p-1 bg-base-900/95 dark:bg-base-800/50 border border-base-700/50 shadow rounded rounded-xl overflow-x-auto";
        preCss += " text-base-100/90  py-2 px-2 md:px-4 ";
    }
    
    renderer.WriteLine("<div class=\"not-prose\">");
    renderer.WriteLine($"<div class=\"{containerCss}\">");
    renderer.WriteLine($"<div class=\"{preCss}\">");
    
    var useDefaultRenderer = true;
    
    if (codeBlock is FencedCodeBlock fencedCodeBlock && 
        codeBlock.Parser is FencedCodeBlockParser fencedCodeBlockParser &&
        fencedCodeBlock.Info != null && 
        fencedCodeBlockParser.InfoPrefix != null)
    {
        var languageId = fencedCodeBlock.Info.Replace(fencedCodeBlockParser.InfoPrefix, string.Empty);
        if (!string.IsNullOrWhiteSpace(languageId))
        {
            var code = ExtractCode(codeBlock);
            useDefaultRenderer = false;

            switch (languageId)
            {
                case "vb" or "vbnet":
                    renderer.Write(roslynHighlighter.Highlight(code, Language.VisualBasic));
                    break;
                case "csharp" or "c#" or "cs":
                    renderer.Write(roslynHighlighter.Highlight(code));
                    break;
                case "csharp:xmldocid,bodyonly":
                    renderer.Write(roslynHighlighter.HighlightExample(code, true));
                    break;
                case "csharp:xmldocid":
                    renderer.Write(roslynHighlighter.HighlightExample(code, false));
                    break;
                case "gbnf":
                    renderer.Write(GbnfHighlighter.HighlightGbnf(code));
                    break;
                default:
                    useDefaultRenderer = true;
                    break;
            }
        }
    }
    
    if (useDefaultRenderer)
    {
        codeBlockRenderer.Write(renderer, codeBlock);
    }
    
    // Common closing tags for all paths
    renderer.WriteLine("</div>");
    renderer.WriteLine("</div>");
    renderer.WriteLine("</div>");
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