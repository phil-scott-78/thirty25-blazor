using Markdig.Renderers;
using Markdig.Renderers.Html;

namespace Thirty25.Web.BlogServices.Markdown;

public class TabBlockRenderer : HtmlObjectRenderer<TabBlock>
{
    protected override void Write(HtmlRenderer renderer, TabBlock block)
    {
        renderer.WriteLine($"<div class=\"tab\" data-title=\"{block.Title}\">");
        renderer.WriteLine($"    <div class=\"tab-heading\">{block.Title}</div>");
        renderer.WriteLine($"    <div class=\"tab-content\">");
        renderer.WriteChildren(block);
        renderer.WriteLine("     </div>");
        renderer.WriteLine("</div>");
    }
}