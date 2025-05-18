using System.Text;
using BlazorStatic.Services.Content.MarkdigExtensions.Tabs;
using BlazorStatic.Services.Content.Roslyn;
using static BlazorStatic.Services.AsyncHelpers;
using Markdig.Parsers;
using Markdig.Renderers.Html;
using Markdig.Syntax;
using HtmlRenderer = Markdig.Renderers.HtmlRenderer;

namespace BlazorStatic.Services.Content.MarkdigExtensions.CodeHighlighting;

internal sealed class CodeHighlightRenderer(
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

        if (codeBlock is FencedCodeBlock fencedCodeBlock &&
            codeBlock.Parser is FencedCodeBlockParser fencedCodeBlockParser &&
            fencedCodeBlock.Info != null &&
            fencedCodeBlockParser.InfoPrefix != null)
        {
            var languageId = fencedCodeBlock.Info.Replace(fencedCodeBlockParser.InfoPrefix, string.Empty);
            var code = ExtractCode(codeBlock);
            WriteCode(renderer, codeBlock, languageId, code);
        }

        // Common closing tags for all paths
        renderer.WriteLine("</div>");
        renderer.WriteLine("</div>");
        renderer.WriteLine("</div>");
    }

    private void WriteCode(HtmlRenderer renderer, CodeBlock codeBlock, string languageId, string code)
    {
        switch (languageId)
        {
            // ReSharper disable SpellCheckingInspection
            case "vb" or "vbnet":
                renderer.Write(roslynHighlighter.Highlight(code, Language.VisualBasic));
                break;
            case "csharp" or "c#" or "cs":
                renderer.Write(roslynHighlighter.Highlight(code));
                break;
            case "csharp:xmldocid,bodyonly":
                var bodyOnlySample = RunSync(async () => await roslynHighlighter.HighlightExampleAsync(code, true));
                renderer.Write(bodyOnlySample);
                break;
            case "csharp:xmldocid":
                var fullSample = RunSync(async () => await roslynHighlighter.HighlightExampleAsync(code, false));
                renderer.Write(fullSample);
                break;
            case "gbnf":
                renderer.Write(GbnfHighlighter.HighlightGbnf(code));
                break;
            default:
            {
                if (languageId.Contains(":xmldocid"))
                {
                    var newLanguage = languageId[..languageId.IndexOf(":xmldocid", StringComparison.Ordinal)];
                    if (codeBlock is not FencedCodeBlock fencedCodeBlock ||
                        !fencedCodeBlock.GetArgumentPairs().TryGetValue("data", out var arg))
                    {
                        arg = string.Empty;
                    }

                    var newCode = RunSync(async () => await roslynHighlighter.GetCodeOutputAsync(code, arg));
                    WriteCode(renderer, codeBlock, newLanguage, newCode);
                }
                else
                {
                    var attr = string.IsNullOrWhiteSpace(languageId)
                        ? string.Empty
                        : " class=\"language-" + languageId + "\"";

                    renderer.Write($"<pre><code {attr}>");
                    renderer.Write(code);
                    renderer.Write("</code></pre>");
                }

                break;
            }
        }
        // ReSharper restore SpellCheckingInspection
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