using Markdig.Renderers;
using Markdig.Renderers.Html;
using Markdig.Syntax;

namespace Thirty25.Web.BlogServices.Markdown;

/// <summary>
/// HTML renderer for the tabbed code block
/// </summary>
internal class TabbedCodeBlockRenderer : HtmlObjectRenderer<TabbedCodeBlock>
{
    // Add a static counter to generate unique tab group names (thread-safe)
    private static int _groupId = 0;
        
    protected override void Write(HtmlRenderer renderer, TabbedCodeBlock obj)
    {
        var codeRenderer = renderer.ObjectRenderers.FindExact<CodeHighlightRenderer>() ?? throw new InvalidOperationException("CodeHighlightRendered should be added to ObjectRenderers");
            
        // Generate a unique group name using Interlocked for thread safety
        var groupName = $"tabs-{Interlocked.Increment(ref _groupId)}";
        var containerCss = "bg-base-900/95 dark:bg-base-800/50 border border-base-700/50 shadow rounded rounded-xl overflow-x-auto";
            
        // Container
        renderer.WriteLine("<div class=\"not-prose\">");
        renderer.WriteLine($"<div class=\"flex flex-col {containerCss}\">");
            
        // Tab buttons
        renderer.WriteLine($"""
                            <div role="tablist" id="tablist{groupName}" aria-orientation="horizontal" class="flex flex-row flex-wrap px-4 pt-1 bg-base-900/95 dark:bg-base-800/50 space-x-4">
                            """);
            
        var tabs = obj.OfType<FencedCodeBlock>().ToList();

        // Create buttons for each code block
        foreach (var (codeBlock, index) in tabs.Select((t, i) => (t, i)))
        {
            var language = string.IsNullOrWhiteSpace(codeBlock.Info) ? "Text" : codeBlock.Info;
            var selected = index == 0 ? "true" : "false";
            var active = index == 0 ? "active" : "inactive";
                
            renderer.WriteLine($"""
                                <button type="button" role="tab" aria-selected="{selected}" aria-controls="tabcontent{groupName}-{index}" data-state="{active}" id="tabButton{groupName}-{index}" class="whitespace-nowrap border-b border-transparent py-2 text-xs text-base-100/90 font-medium transition-colors hover:text-accent-300 disabled:pointer-events-none disabled:opacity-50 aria-selected:text-accent-400 aria-selected:border-accent-400 " tabindex="-1" data-orientation="horizontal">{language}</button>
                                """);
        }
            
        renderer.WriteLine("</div>");
            
        // Tab content panels
        foreach (var (codeBlock, index) in tabs.Select((t, i) => (t, i)))
        {
            // Content panel with aria attributes
            renderer.WriteLine($"""
                                <div aria-labelledby="tabButton{groupName}-{index}" id="tabcontent{groupName}-{index}" class="tab-panel hidden aria-selected:block py-3 px-2 md:px-4 ">
                                """);

            codeRenderer.Write(renderer, codeBlock);
            // renderer.WriteChildren(codeBlock);
            renderer.WriteLine("</div>");
        }
            
        renderer.WriteLine("</div>");
        renderer.WriteLine("</div>");
    }
}