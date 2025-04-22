using Markdig;
using Markdig.Renderers;

namespace Thirty25.Web.BlogServices.Markdown;

public class PymdownBlocksExtension : IMarkdownExtension
{
    public void Setup(MarkdownPipelineBuilder pipeline)
    {
        // 1) add your fence parser
        pipeline.BlockParsers.Insert(0, new GenericContainerParser());

        // 2) hook the post‐parse event
        pipeline.DocumentProcessed += document =>
        {
            // Bail early if there are no TabBlocks at the top level
            // We only scan top-level blocks, so nested tabs won't be detected.
            if (document.All(b => b is not TabBlock))
                return;

            // Why this rebuild is needed:
            // TabBlocks already belong to the document (i.e., they have a Parent set),
            // and Markdig does NOT allow a block to be re-parented once attached.
            // To safely group them, we clone the document structure:
            // 1. Copy all top-level blocks into a list
            // 2. Clear the document
            // 3. Rebuild the document, grouping consecutive TabBlocks into a TabsContainerBlock

            var allBlocks = document.ToList();
            document.Clear();

            for (var i = 0; i < allBlocks.Count;)
            {
                if (allBlocks[i] is TabBlock)
                {
                    var group = new TabsContainerBlock();

                    // Collect all adjacent TabBlocks and move them into the TabsContainerBlock
                    while (i < allBlocks.Count && allBlocks[i] is TabBlock tab)
                    {
                        group.Add(tab);  // safe because we're no longer iterating over the document directly
                        i++;
                    }

                    document.Add(group);
                }
                else
                {
                    document.Add(allBlocks[i]);
                    i++;
                }
            }
        };
    }
    

    public void Setup(MarkdownPipeline pipeline, IMarkdownRenderer renderer)
    {
        if (renderer is HtmlRenderer html)
        {
            html.ObjectRenderers.Add(new TabsContainerRenderer());
            html.ObjectRenderers.Add(new TabBlockRenderer());
            html.ObjectRenderers.Add(new AdmonitionBlockRenderer());
        }
    }
}