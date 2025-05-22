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
        var proseSettings = GetCustomProseSettings();


        var primaryHue = 3;
        var primary = ColorPaletteGenerator.GenerateFromHue(primaryHue);
        var accent = ColorPaletteGenerator.GenerateFromHue(primaryHue + 180);

        var tertiaryOne = ColorPaletteGenerator.GenerateFromHue(primaryHue + 90);
        var tertiaryTwo = ColorPaletteGenerator.GenerateFromHue(primaryHue - 90);
        
        return new CssFramework(new CssFrameworkSettings
        {
            DesignSystem = DesignSystem.Default with
            {
                Colors = DesignSystem.Default.Colors.AddRange(
                    new Dictionary<string, ImmutableDictionary<string, CssColor>>
                    {
                        { "primary", primary },
                        { "accent", accent },
                        { "tertiary-one", tertiaryOne },
                        { "tertiary-two", tertiaryTwo },
                        { "base", DesignSystem.Default.Colors[ColorNames.Gray] }
                    })
            },
            PluginSettings = new List<ISettings> { proseSettings },
            Applies =
                new
                    Dictionary<string, string> /* these are just for a custom starry-night theme using tailwind colors */
                    {
                        { "code", "font-mono" },
                        { ".prose h1, .prose h2, .prose h3, .prose h4", "scroll-m-24" }, // this is to offset the header

                        { ".pl-c", "text-base-800/50 italic" },
                        { ".pl-cd, .pl-cmnt, .pl-pds, .pl-sel, .pl-tag", "text-base-800" }, // comments, punctuation, selectors, tags
                        { ".pl-c1, .pl-en, .pl-entm", "text-primary-700" }, // boolean, number, constants, attributes, deleted
                        { ".pl-s, .pl-pse, .pl-smi, .pl-smp", "text-tertiary-one-700" }, // strings, characters, attribute values, builtins, inserted
                        { ".pl-kos, .pl-ent, .pl-v, .pl-sym, .pl-e, .pl-cce", "text-tertiary-two-700" }, // operators, entities, urls, symbols, class names
                        { ".pl-k, .pl-kd", "text-primary-700" }, // atrules, keywords
                        { ".pl-c1, .pl-en", "text-accent-700" }, // properties, functions
                        { ".pl-sr, .pl-va", "text-red-700" }, // regex, important
                        
                        { ".dark .pl-c", "text-base-300/50 italic" },
                        { ".dark .pl-cd, .dark .pl-cmnt, .dark .pl-pds, .dark .pl-sel, .dark .pl-tag", "text-base-300" }, // comments, punctuation, selectors, tags
                        { ".dark .pl-c1, .dark .pl-en, .dark .pl-entm", "text-primary-300" }, // boolean, number, constants, attributes, deleted
                        { ".dark .pl-s, .dark.pl-pse, .dark .pl-smi, .dark .pl-smp", "text-tertiary-one-300" }, // strings, characters, attribute values, builtins, inserted
                        { ".dark .pl-kos, .dark .pl-ent, .dark .pl-v, .dark .pl-sym,.dark .pl-e,.dark .pl-cce", "text-tertiary-two-300" }, // operators, entities, urls, symbols, class names
                        { ".dark .pl-k, .dark .pl-kd", "text-primary-300" }, // atrules, keywords
                        { ".dark .pl-c1, .dark .pl-en", "text-accent-300" }, // properties, functions
                        { ".dark .pl-sr, .dark .pl-va", "text-red-300" }, // regex, important
                        
                    }
        });
    }

    private static Prose.Settings GetCustomProseSettings()
    {
        var proseSettings = new Prose.Settings
        {
            CustomSettings = designSystem => new Dictionary<string, CssSettings>
            {
                {
                    "DEFAULT", new CssSettings
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
                                new CssDeclaration(CssProperties.BackgroundColor,
                                    designSystem.Colors["accent"][ColorLevels._200].AsStringWithOpacity(".50")),
                                new CssDeclaration(CssProperties.Color,
                                    designSystem.Colors["base"][ColorLevels._700].AsString()),
                            ]),
                        ]
                    }
                },
                {
                    "lg", new CssSettings
                    {
                        ChildRules =
                        [
                            new CssRuleSet("code", [new CssDeclaration(CssProperties.FontSize, "0.8em")])
                        ]
                    }
                },
                {
                    // dark mode color overrides
                    "invert", new CssSettings
                    {
                        ChildRules =
                        [
                            new CssRuleSet("code",
                            [
                                new CssDeclaration(CssProperties.BackgroundColor,
                                    designSystem.Colors["base"][ColorLevels._800].AsStringWithOpacity(".75")),
                                new CssDeclaration(CssProperties.Color,
                                    designSystem.Colors["accent"][ColorLevels._400].AsString()),
                            ])
                        ]
                    }
                },
            }.ToImmutableDictionary()
        };
        return proseSettings;
    }
}