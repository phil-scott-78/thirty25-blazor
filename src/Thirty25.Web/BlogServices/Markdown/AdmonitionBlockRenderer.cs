using Markdig.Renderers;
using Markdig.Renderers.Html;

namespace Thirty25.Web.BlogServices.Markdown;

public class AdmonitionBlockRenderer : HtmlObjectRenderer<AdmonitionBlock>
{
    protected override void Write(HtmlRenderer renderer, AdmonitionBlock block)
    {
        var type = block.AdmonitionType;
        var title = string.IsNullOrEmpty(block.Title)
            ? char.ToUpper(type[0]) + type.Substring(1)
            : block.Title;

        renderer.Write($"<div class=\"admonition {type}\">")
            .Write($"<p class=\"admonition-title\">")
            .WriteEscape(title)
            .Write("</p>");
        renderer.WriteChildren(block);
        renderer.Write("</div>");
    }
}