using Markdig.Renderers;
using Markdig.Renderers.Html;
using System.Linq;
using System.Threading;
using Markdig.Syntax;

namespace Thirty25.Web.BlogServices.Markdown;

public class TabsContainerRenderer : HtmlObjectRenderer<TabsContainerBlock>
{
    // Add a static counter to generate unique tab group names
    private static int _groupId = 0;

    protected override void Write(HtmlRenderer renderer, TabsContainerBlock block)
    {
        // Generate a unique group name for this tabs container
        var groupName = $"tabs-{Interlocked.Increment(ref _groupId)}";
        var tabs = block.OfType<TabBlock>().ToList();
        if (!tabs.Any()) return;

        // Container
        renderer.WriteLine("<div class=\"flex flex-row flex-wrap\">");
        // For each tab: radio, label, and panel interleaved for proper peer relation
        foreach (var (tab, index) in tabs.Select((t, i) => (t, i)))
        {
            var checkedAttr = index == 0 ? "checked" : string.Empty;
            // Radio input
            renderer.WriteLine($"<input type=\"radio\" name=\"{groupName}\" id=\"{groupName}-{index}\" class=\"hidden peer\" {checkedAttr}/>");
            // Tab heading label
            renderer.WriteLine($"<label for=\"{groupName}-{index}\" class=\"cursor-pointer px-4 py-2 bg-gray-200 mr-2 peer-checked:bg-blue-500 peer-checked:text-white\">{tab.Title}</label>");
            // Content panel
            renderer.WriteLine("<div class=\"tab-panel hidden peer-checked:block\">");
            renderer.WriteChildren(tab);
            renderer.WriteLine("</div>");
        }
        renderer.WriteLine("</div>");
    }
}