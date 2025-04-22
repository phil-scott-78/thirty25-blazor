using Markdig.Parsers;

namespace Thirty25.Web.BlogServices.Markdown;

public class TabBlock(BlockParser parser, string title) : GenericContainerBlock(parser, title);