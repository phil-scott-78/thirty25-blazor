using Markdig.Parsers;
using Markdig.Syntax;

namespace Thirty25.Web.BlogServices.Markdown;

public abstract class GenericContainerBlock(BlockParser parser, string title) : ContainerBlock(parser)
{
    public string Title { get; } = title;
}
