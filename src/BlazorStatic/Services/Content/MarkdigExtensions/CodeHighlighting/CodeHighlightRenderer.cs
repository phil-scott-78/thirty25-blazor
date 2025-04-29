using System.Text;
using BlazorStatic.Services.Content.MarkdigExtensions.Tabs;
using BlazorStatic.Services.Content.Roslyn;
using Markdig.Parsers;
using Markdig.Renderers;
using Markdig.Renderers.Html;
using Markdig.Syntax;

namespace BlazorStatic.Services.Content.MarkdigExtensions.CodeHighlighting;

internal sealed class CodeHighlightRenderer(
    CodeBlockRenderer codeBlockRenderer,
    RoslynHighlighterService roslynHighlighter,
    CodeHighlightRenderOptions? options = null)
    : HtmlObjectRenderer<CodeBlock>
{
    private CodeHighlightRenderOptions? _options = options;

    protected override void Write(HtmlRenderer renderer, CodeBlock codeBlock)
    {
        _options ??= CodeHighlightRenderOptions.Default;

        var preCss = _options.PreBaseCss;
        var containerCss = "";

        if (codeBlock.Parent is not TabbedCodeBlock)
        {
            // if we aren't in a tab block, then let's create ourselves a container
            containerCss = _options.StandaloneContainerCss;
            preCss += $" {_options.PreStandaloneCss} ";
        }

        renderer.WriteLine($"<div class=\"{_options.OuterWrapperCss}\">");
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

                // ReSharper disable SpellCheckingInspection
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
                // ReSharper restore SpellCheckingInspection
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