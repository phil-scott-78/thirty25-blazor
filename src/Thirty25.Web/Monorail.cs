using System.Collections.Immutable;
using System.Text.RegularExpressions;
using MonorailCss;
using MonorailCss.Css;
using MonorailCss.Plugins;
using MonorailCss.Plugins.Prose;

namespace Thirty25.Web;

internal partial class MonorailCssService(IWebHostEnvironment env)
{
    public async Task<string> GetStyleSheet()
    {
        // we are only scanning razor files, not the generated files. if you use
        // code like bg-{color}-400 in the razor as a variable, that's not gonna be detected.
        var cssClassValues = await ScanRazorFilesForCssClasses();
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
                                    new CssDeclaration(CssProperties.BorderColor, designSystem.Colors[ColorNames.Neutral][ColorLevels._700].AsStringWithOpacity("50%")),
                                    new CssDeclaration(CssProperties.BorderWidth, "1px"),
                                    new CssDeclaration(CssProperties.FontWeight, "300")
                                ])
                        ]
                    }
                }
            }.ToImmutableDictionary()
        };

        var (primary, accent) = ColorPaletteGenerator.GenerateFromHue(24);

        return new CssFramework(new CssFrameworkSettings()
            {
                DesignSystem = DesignSystem.Default with
                {
                    Colors = DesignSystem.Default.Colors.AddRange(
                        new Dictionary<string, ImmutableDictionary<string, CssColor>>()
                        {
                            { "primary", primary },
                            { "accent", accent },
                            { "base", DesignSystem.Default.Colors[ColorNames.Neutral] }
                        })
                },
                PluginSettings = new List<ISettings> { proseSettings },
            });
    }

    private async Task<HashSet<string>> ScanRazorFilesForCssClasses()
    {
        var contentRootPath = env.ContentRootPath;
        var razorFiles = Directory.GetFiles(contentRootPath, "*.razor", SearchOption.AllDirectories)
            .Concat(Directory.GetFiles(contentRootPath, "*.cshtml", SearchOption.AllDirectories)).ToArray();

        // CSS class pattern - looking for class="..." or class='...' patterns
        var classRegex = RazorClassRegex();

        var values = new HashSet<string>();
        foreach (var file in razorFiles)
        {
            var content = await File.ReadAllTextAsync(file);
            var matches = classRegex.Matches(content);

            foreach (Match match in matches)
            {
                var classValue = match.Groups["value"].Value;
                // Split by whitespace to get individual class names
                var individualClasses = classValue.Split([' '], StringSplitOptions.RemoveEmptyEntries);

                foreach (var className in individualClasses)
                {
                    values.Add(className.Trim());
                }
            }
        }

        return values;
    }

    [GeneratedRegex(
        """(class\s*=\s*[\'\"](?<value>[^<]*?)[\'\"])|(cssclass\s*=\s*[\'\"](?<value>[^<]*?)[\'\"])|(CssClass\s*\(\s*\"(?<value>[^<]*?)\"\s*\))""",
        RegexOptions.Compiled)]
    private static partial Regex RazorClassRegex();
}