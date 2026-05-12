using System.Net;
using System.Text;
using Pennington.Highlighting;

namespace Thirty25.Web;

public sealed class GbnfHighlighter : ICodeHighlighter
{
    public IReadOnlySet<string> SupportedLanguages { get; } =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "gbnf" };

    public int Priority => 100;

    public string Highlight(string code, string language)
    {
        if (string.IsNullOrEmpty(code))
        {
            return "<pre><code></code></pre>";
        }

        var tokens = Tokenize(code);
        MarkRuleNames(tokens);

        var sb = new StringBuilder(code.Length * 2);
        sb.Append("<pre><code>");
        foreach (var token in tokens)
        {
            var encoded = WebUtility.HtmlEncode(token.Text);
            var css = CssClassFor(token.Kind);
            if (css is null)
            {
                sb.Append(encoded);
            }
            else
            {
                sb.Append("<span class=\"").Append(css).Append("\">").Append(encoded).Append("</span>");
            }
        }
        sb.Append("</code></pre>");
        return sb.ToString();
    }

    private static string? CssClassFor(TokenKind kind) => kind switch
    {
        TokenKind.Comment => "hljs-comment",
        TokenKind.RuleOperator => "hljs-keyword",
        TokenKind.String => "hljs-string",
        TokenKind.CharClass => "hljs-regexp",
        TokenKind.Token => "hljs-symbol",
        TokenKind.Identifier => "hljs-variable",
        TokenKind.RuleName => "hljs-title",
        TokenKind.Operator => "hljs-operator",
        _ => null
    };

    private static void MarkRuleNames(List<Token> tokens)
    {
        for (var i = 0; i < tokens.Count; i++)
        {
            if (tokens[i].Kind != TokenKind.Identifier)
            {
                continue;
            }

            var j = i + 1;
            while (j < tokens.Count && tokens[j].Kind == TokenKind.Whitespace)
            {
                j++;
            }

            if (j < tokens.Count && tokens[j].Kind == TokenKind.RuleOperator)
            {
                tokens[i] = tokens[i] with { Kind = TokenKind.RuleName };
            }
        }
    }

    private enum TokenKind
    {
        Whitespace,
        Comment,
        RuleOperator,
        String,
        CharClass,
        Token,
        Identifier,
        RuleName,
        Operator,
        Other
    }

    private record struct Token(TokenKind Kind, string Text);

    private static List<Token> Tokenize(string s)
    {
        var tokens = new List<Token>();
        var i = 0;
        while (i < s.Length)
        {
            var c = s[i];

            if (char.IsWhiteSpace(c))
            {
                var start = i;
                while (i < s.Length && char.IsWhiteSpace(s[i]))
                {
                    i++;
                }
                tokens.Add(new Token(TokenKind.Whitespace, s[start..i]));
                continue;
            }

            if (c == '#')
            {
                var start = i;
                while (i < s.Length && s[i] != '\n')
                {
                    i++;
                }
                tokens.Add(new Token(TokenKind.Comment, s[start..i]));
                continue;
            }

            if (c == ':' && i + 2 < s.Length && s[i + 1] == ':' && s[i + 2] == '=')
            {
                tokens.Add(new Token(TokenKind.RuleOperator, "::="));
                i += 3;
                continue;
            }

            if (c == '"')
            {
                var start = i++;
                while (i < s.Length && s[i] != '"')
                {
                    if (s[i] == '\\' && i + 1 < s.Length)
                    {
                        i += 2;
                    }
                    else
                    {
                        i++;
                    }
                }
                if (i < s.Length)
                {
                    i++;
                }
                tokens.Add(new Token(TokenKind.String, s[start..i]));
                continue;
            }

            if (c == '[')
            {
                var start = i++;
                while (i < s.Length && s[i] != ']')
                {
                    if (s[i] == '\\' && i + 1 < s.Length)
                    {
                        i += 2;
                    }
                    else
                    {
                        i++;
                    }
                }
                if (i < s.Length)
                {
                    i++;
                }
                tokens.Add(new Token(TokenKind.CharClass, s[start..i]));
                continue;
            }

            if (c == '!' && i + 1 < s.Length && s[i + 1] == '<')
            {
                var start = i;
                i += 2;
                while (i < s.Length && s[i] != '>')
                {
                    i++;
                }
                if (i < s.Length)
                {
                    i++;
                }
                tokens.Add(new Token(TokenKind.Token, s[start..i]));
                continue;
            }

            if (c == '<')
            {
                var start = i++;
                while (i < s.Length && s[i] != '>')
                {
                    i++;
                }
                if (i < s.Length)
                {
                    i++;
                }
                tokens.Add(new Token(TokenKind.Token, s[start..i]));
                continue;
            }

            if (c is '|' or '?' or '*' or '+' or '(' or ')')
            {
                tokens.Add(new Token(TokenKind.Operator, c.ToString()));
                i++;
                continue;
            }

            if (c == '{')
            {
                var start = i++;
                while (i < s.Length && s[i] != '}')
                {
                    i++;
                }
                if (i < s.Length)
                {
                    i++;
                }
                tokens.Add(new Token(TokenKind.Operator, s[start..i]));
                continue;
            }

            if (char.IsLetter(c) || c == '_')
            {
                var start = i;
                while (i < s.Length && (char.IsLetterOrDigit(s[i]) || s[i] == '_' || s[i] == '-'))
                {
                    i++;
                }
                tokens.Add(new Token(TokenKind.Identifier, s[start..i]));
                continue;
            }

            tokens.Add(new Token(TokenKind.Other, c.ToString()));
            i++;
        }
        return tokens;
    }
}
