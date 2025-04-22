using Markdig.Parsers;

namespace Thirty25.Web.BlogServices.Markdown;

public class AdmonitionBlock(BlockParser parser, string type, string title) : GenericContainerBlock(parser, title)
{
    public string AdmonitionType { get; } = type;
}