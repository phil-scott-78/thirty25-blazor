using System.Text.RegularExpressions;
using Markdig.Parsers;
using Markdig.Syntax;

namespace Thirty25.Web.BlogServices.Markdown;

public partial class GenericContainerParser : BlockParser
{
    private static readonly Regex OpenFence = OpenFenceRegex();

    public GenericContainerParser()
    {
        OpeningCharacters = ['/'];
    }

    public override BlockState TryOpen(BlockProcessor proc)
    {
        var line = proc.Line.ToString();
        var m = OpenFence.Match(line);
        if (!m.Success) return BlockState.None;

        var name = m.Groups[1].Value; // "tab" or "admonition" or shortcut
        var title = m.Groups[2].Value;

        if (name == "tab")
        {
            var tab = new TabBlock(this, title);
            proc.NewBlocks.Push(tab);
            proc.GoToColumn(proc.Line.End);
            return BlockState.ContinueDiscard;
        }

        if (name == "admonition" || IsShortcutAdmonition(name))
        {
            var admon = new AdmonitionBlock(this, name, title);
            proc.NewBlocks.Push(admon);
            proc.GoToColumn(proc.Line.End);
            return BlockState.ContinueDiscard;
        }

        return BlockState.None;
    }

    public override BlockState TryContinue(BlockProcessor proc, Block block)
    {
        var text = proc.Line.ToString().TrimStart();
        if (TryContinueRegex().IsMatch(text))
        {
            proc.Close(block);
            proc.GoToColumn(proc.Line.End);
            return BlockState.ContinueDiscard;
        }

        return BlockState.Continue;
    }

    private static bool IsShortcutAdmonition(string name) =>
        name is "note" or "tip" or "warning" or "caution" or "danger" or "info";
    
    [GeneratedRegex(@"^/{3,}\s*$")]
    private static partial Regex TryContinueRegex();
    [GeneratedRegex(@"^ {0,3}/{3,}\s*(\w+)(?:\s*\|\s*([^\r\n]+?))?\s*$", RegexOptions.Compiled)]
    private static partial Regex OpenFenceRegex();
}