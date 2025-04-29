using System.Collections.Immutable;
using MonorailCss;
using MonorailCss.Css;
using MonorailCss.Plugins;
using MonorailCss.Plugins.Prose;


namespace Thirty25.Web.BlogServices.Styling;

internal class MonorailCssService(CssClassCollector cssClassCollector)
{
    public string GetStyleSheet()
    {
        // we are only scanning razor files, not the generated files. if you use
        // code like bg-{color}-400 in the razor as a variable, that's not going to be detected.
        var cssClassValues = cssClassCollector.GetClasses();
        return GetCssFramework().Process(cssClassValues);
    }

    private static CssFramework GetCssFramework()
    {
        var proseSettings = new Prose.Settings()
        {
            CustomSettings = designSystem => new Dictionary<string, CssSettings>()
            {
                {
                    "DEFAULT", new CssSettings()
                    {
                        ChildRules =
                        [
                            new CssRuleSet("a",
                            [
                                new CssDeclaration(CssProperties.FontWeight, "inherit"),
                                new CssDeclaration(CssProperties.TextDecoration, "none"),
                                new CssDeclaration(CssProperties.BorderBottomWidth, "1px"),
                                new CssDeclaration(CssProperties.BorderBottomColor,
                                    designSystem.Colors["primary"][ColorLevels._500].AsStringWithOpacity("75%"))
                            ]),
                            new CssRuleSet("pre",
                            [
                                new CssDeclaration(CssProperties.BorderRadius, "10px"),
                                new CssDeclaration(CssProperties.BorderColor,
                                    designSystem.Colors["base"][ColorLevels._700].AsStringWithOpacity("50%")),
                                new CssDeclaration(CssProperties.FontFeatureSettings,
                                    "\"cv02\", \"cv03\", \"cv04\", \"cv11\""),
                                new CssDeclaration(CssProperties.BorderWidth, "1px"),
                                new CssDeclaration(CssProperties.FontWeight, "300"),
                            ]),
                            new CssRuleSet("blockquote",
                            [
                                new CssDeclaration(CssProperties.BorderLeftWidth, "4px"),
                                new CssDeclaration(CssProperties.PaddingLeft, "1rem"),
                                new CssDeclaration(CssProperties.BorderColor,
                                    designSystem.Colors["primary"][ColorLevels._700].AsString()),
                            ]),
                            new CssRuleSet("code",
                            [
                                new CssDeclaration(CssProperties.FontSize, ".80em"),
                                new CssDeclaration(CssProperties.FontWeight, "400"),
                                new CssDeclaration(CssProperties.Padding, "2px 5px"),
                                new CssDeclaration(CssProperties.BorderRadius, "4px"),
                                new CssDeclaration(CssProperties.BackgroundColor, designSystem.Colors["accent"][ColorLevels._200].AsStringWithOpacity(".50")),
                                new CssDeclaration(CssProperties.Color, designSystem.Colors["base"][ColorLevels._700].AsString()),
                            ]),
                        ]
                    }
                },
                {
                    // dark mode color overrides
                    "invert", new CssSettings()
                    {
                        ChildRules =
                        [
                            new CssRuleSet("code",
                            [
                                new CssDeclaration(CssProperties.BackgroundColor, designSystem.Colors["base"][ColorLevels._800].AsStringWithOpacity(".75")),
                                new CssDeclaration(CssProperties.Color, designSystem.Colors["accent"][ColorLevels._400].AsString()),
                            ])
                        ]
                    }
                },
            }.ToImmutableDictionary()
        };


        var (primary, accent) = ColorPaletteGenerator.GenerateFromHue(235);

        return new CssFramework(new CssFrameworkSettings()
        {
            DesignSystem = DesignSystem.Default with
            {
                Colors = DesignSystem.Default.Colors.AddRange(
                    new Dictionary<string, ImmutableDictionary<string, CssColor>>()
                    {
                        { "primary", primary },
                        { "accent", accent },
                        { "base", DesignSystem.Default.Colors[ColorNames.Slate] }
                    })
            },
            PluginSettings = new List<ISettings> { proseSettings },
            Applies =
                new
                    Dictionary<string, string> /* these are just for a custom starry-night theme using tailwind colors */
                    {
                        { "code", "font-mono" },
                        { ".prose h1, .prose h2, .prose h3, .prose h4", "scroll-m-24" },
                        { ".pl-c", "text-base-300/50 italic" },
                        {
                            ".pl-cd, .pl-cmnt, .pl-pds, .pl-sel, .pl-tag", "text-base-300"
                        }, // comments, punctuation, selectors, tags
                        {
                            ".pl-c1, .pl-en, .pl-entm", "text-blue-300"
                        }, // boolean, number, constants, attributes, deleted
                        {
                            ".pl-s, .pl-pse, .pl-smi, .pl-smp", "text-green-300"
                        }, // strings, characters, attribute values, builtins, inserted
                        {
                            ".pl-kos, .pl-ent, .pl-v, .pl-sym, .pl-e, .pl-cce", "text-cyan-300"
                        }, // operators, entities, urls, symbols, class names
                        { ".pl-k, .pl-kd", "text-indigo-300" }, // atrules, keywords
                        { ".pl-c1, .pl-en", "text-orange-300" }, // properties, functions
                        { ".pl-sr, .pl-va", "text-red-300" }, // regex, important
                    }
        });
    }
}