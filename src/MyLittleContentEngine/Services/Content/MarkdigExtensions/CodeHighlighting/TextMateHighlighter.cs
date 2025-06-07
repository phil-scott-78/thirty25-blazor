using System.Net;
using System.Text;
using TextMateSharp.Grammars;
using TextMateSharp.Registry;

namespace MyLittleContentEngine.Services.Content.MarkdigExtensions.CodeHighlighting;

public static class TextMateHighlighter
{
    private static readonly Registry Registry;
    private static readonly RegistryOptions RegistryOptions;
    private static readonly List<Tuple<string, string>> ScopeMappings;
    private static readonly TimeSpan TokenizeTimeLimit = TimeSpan.FromSeconds(5); // Default time limit for tokenization
    private static readonly Lock RegistryAccessLock = new Lock(); // Added for thread safety

    static TextMateHighlighter()
    {
        // Initialize the TextMate registry with grammars.
        RegistryOptions = new RegistryOptions(ThemeName.DarkPlus);
        Registry = new Registry(RegistryOptions);

        // Initialize the mapping from TextMate scopes to "pl-*" CSS classes.
        ScopeMappings =
        [
            Tuple.Create("comment.line.double-slash", "pl-c"), // C#, Java, JS comments
            Tuple.Create("comment.block.documentation", "pl-cd"), // Documentation comments
            Tuple.Create("comment.block", "pl-c"),
            Tuple.Create("comment", "pl-c"),
            Tuple.Create("punctuation.definition.comment", "pl-c"),

            // Entities (functions, types, tags, attributes)
            Tuple.Create("entity.name.function", "pl-en"), // functions
            Tuple.Create("entity.name.type", "pl-e"), // types (classes, structs, interfaces)
            Tuple.Create("entity.name.class", "pl-e"),
            Tuple.Create("entity.name.interface", "pl-e"),
            Tuple.Create("entity.name.struct", "pl-e"),
            Tuple.Create("entity.name.enum", "pl-e"),
            Tuple.Create("entity.name.tag", "pl-tag"), // HTML/XML tags
            Tuple.Create("entity.other.attribute-name", "pl-e"), // HTML/XML attributes
            Tuple.Create("entity.other.inherited-class", "pl-e"),
            Tuple.Create("meta.attribute.cs", "pl-en"), // C# attributes

            // Keywords
            Tuple.Create("keyword.control", "pl-k"),
            Tuple.Create("keyword.operator.new", "pl-k"), // `new` operator
            Tuple.Create("keyword.operator", "pl-kos"), // other operators
            Tuple.Create("keyword", "pl-k"), // general keywords

            // Storage (type keywords, modifiers)
            Tuple.Create("storage.type", "pl-k"), // `int`, `string`, `var`
            Tuple.Create("storage.modifier", "pl-k"), // `public`, `static`, `async`

            // Constants and Literals
            Tuple.Create("constant.numeric", "pl-c1"), // numbers
            Tuple.Create("constant.language", "pl-c1"), // `true`, `false`, `null`
            Tuple.Create("constant.character.escape", "pl-pse"), // character escapes (e.g. \\n, \\t)
            Tuple.Create("constant.other", "pl-c1"),

            // Strings
            Tuple.Create("string.quoted.interpolated", "pl-s"), // interpolated string content
            Tuple.Create("string.regexp", "pl-sr"), // regular expressions
            Tuple.Create("string", "pl-s"), // general strings
            Tuple.Create("punctuation.definition.string", "pl-s"), // quotes for strings

            // Punctuation
            Tuple.Create("punctuation.definition.tag", "pl-tag"), // <, > in HTML/XML
            Tuple.Create("punctuation.separator", "pl-pds"), // commas, colons
            Tuple.Create("punctuation.terminator", "pl-pds"), // semicolons
            Tuple.Create("punctuation.accessor", "pl-pds"), // . in member access
            Tuple.Create("punctuation.section.embedded.begin", "pl-pse"), // e.g. { in C# interpolated string
            Tuple.Create("punctuation.section.embedded.end", "pl-pse"), // e.g. } in C# interpolated string
            Tuple.Create("punctuation", "pl-pds"), // General punctuation

            // Variables
            Tuple.Create("variable.parameter", "pl-v"),
            Tuple.Create("variable.language", "pl-v"), // `this`, `self`
            Tuple.Create("variable.other.member", "pl-smi"), // instance variables/members
            Tuple.Create("variable.other.object.property", "pl-smi"), // object properties
            Tuple.Create("variable.other.constant", "pl-c1"), // constants defined as variables
            Tuple.Create("variable.other.enummember", "pl-smi"), // enum members
            Tuple.Create("variable", "pl-v"), // general variables

            // Support (built-in functions, classes, constants)
            Tuple.Create("support.function", "pl-en"),
            Tuple.Create("support.class", "pl-e"),
            Tuple.Create("support.type", "pl-k"), // primitive types if not `storage.type`
            Tuple.Create("support.constant", "pl-c1"),

            // Markup
            Tuple.Create("markup.inserted", "pl-smp"),
            Tuple.Create("markup.deleted", "pl-entm"),

            // Meta scopes
            Tuple.Create("meta.selector", "pl-sel"), // CSS selectors
            Tuple.Create("meta.tag", "pl-tag"),
            Tuple.Create("meta.definition.method", "pl-en"), // For method definitions
            Tuple.Create("meta.definition.type", "pl-e"), // For class/struct/interface definitions

            // Fallback
            Tuple.Create("entity", "pl-ent")
        ];
    }

    private static string? GetPlClassForScopes(List<string> scopes)
    {
        if (scopes.Count == 0)
            return null;

        for (var i = scopes.Count - 1; i >= 0; i--)
        {
            var currentScope = scopes[i];
            foreach (var mapping in ScopeMappings.Where(mapping => currentScope.StartsWith(mapping.Item1)))
            {
                return mapping.Item2;
            }
        }

        return null; // No mapping found
    }

    public static string Highlight(string text, string language)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        IGrammar? grammar = null;

        // the registry doesn't seem to be thread-safe, so we need to lock access to it
        lock (RegistryAccessLock)
        {
            var scopeName = RegistryOptions.GetScopeByLanguageId(language.ToLowerInvariant());

            if (!string.IsNullOrEmpty(scopeName))
            {
                grammar = Registry.LoadGrammar(scopeName);
            }

            // Attempt a broader search if a specific scopeName not found or language ID is an alias
            if (grammar == null)
            {
                // Try common conventions like "source.{lang}" or just the language name if it's a known short form
                // TextMateSharp's grammars often use "source.{language_name}"
                var potentialScopeNames = new List<string>
                {
                    $"source.{language.ToLowerInvariant()}",
                    language.ToLowerInvariant() // Some grammars might be registered with short names
                };

                foreach (var potentialScope in potentialScopeNames)
                {
                    try
                    {
                        grammar = Registry.LoadGrammar(potentialScope);
                        if (grammar != null) break;
                    }
                    catch
                    {
                        // Ignore exceptions if a potential scope name is invalid for lookup
                    }
                }
            }

            // If no grammar was found, return the text as a plain code block
            if (grammar == null)
            {
                var escapedCode = WebUtility.HtmlEncode(text);
                return $"<pre><code class=\"language-{language} code\">{escapedCode}</code></pre>";
            }

            var sb = new StringBuilder();
            sb.Append("<pre><code>");


            var lines = text.SplitNewLines();
            IStateStack? ruleStack = null; // IRawStack can be null initially

            for (var i = 0; i < lines.Length; i++)
            {
                var line = lines[i];
                // Pass the time limit to TokenizeLine
                var result = grammar.TokenizeLine(line, ruleStack, TokenizeTimeLimit);
                ruleStack = result.RuleStack;

                var currentIndex = 0;
                foreach (var token in result.Tokens)
                {
                    if (token.StartIndex > currentIndex)
                    {
                        sb.Append(WebUtility.HtmlEncode(line.Substring(currentIndex, token.StartIndex - currentIndex)));
                    }

                    // Ensure token.Length does not exceed the line boundary
                    var length = token.Length;
                    if (token.StartIndex + length > line.Length)
                    {
                        length = line.Length - token.StartIndex;
                    }

                    var tokenText = line.Substring(token.StartIndex, length);
                    var escapedTokenText = WebUtility.HtmlEncode(tokenText);
                    var plClass = GetPlClassForScopes(token.Scopes);

                    if (!string.IsNullOrEmpty(plClass))
                    {
                        sb.Append($"<span class=\"{plClass}\">{escapedTokenText}</span>");
                    }
                    else
                    {
                        sb.Append(escapedTokenText);
                    }

                    currentIndex = token.StartIndex + length;
                }

                if (i < lines.Length - 1)
                {
                    sb.AppendLine();
                }
            }

            sb.Append("</code></pre>");
            return sb.ToString();
        }
    }
}