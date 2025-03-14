﻿using System.Collections.Immutable;
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
                                    designSystem.Colors[ColorNames.Blue][ColorLevels._500].AsStringWithOpacity("75%"))
                            ]),
                            new CssRuleSet("pre",
                                [
                                    new CssDeclaration(CssProperties.BorderRadius, "10px"),
                                    ])
                        ]
                    }
                }
            }.ToImmutableDictionary()
        };

        var (primary, accent) = ColorPaletteGenerator.GenerateFromHue(310);

        return new CssFramework(new CssFrameworkSettings()
            {
                DesignSystem = DesignSystem.Default with
                {
                    Colors = DesignSystem.Default.Colors.AddRange(
                        new Dictionary<string, ImmutableDictionary<string, CssColor>>()
                        {
                            { "primary", primary },
                            { "accent", accent },
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

public static class ColorPaletteGenerator
{
    // The provided chroma and lightness values from https://evilmartians.com/chronicles/better-dynamic-themes-in-tailwind-with-oklch-color-magic
    private static readonly double[] ChromaLevels =
    [
        0.0108, 0.0321, 0.0609, 0.0908, 0.1398, 0.1472, 0.1299, 0.1067, 0.0898, 0.0726, 0.054
    ];
    
    private static readonly double[] LightnessLevels =
    [
        97.78, 93.56, 88.11, 82.67, 74.22, 64.78, 57.33, 46.89, 39.44, 32.00, 23.78
    ];
    
    // Keys for the palette
    private static readonly string[] PaletteKeys =
    [
        "50", "100", "200", "300", "400", "500", "600", "700", "800", "900", "950"
    ];
    
    /// <summary>
    /// Generates a primary and accent color palette based on a hue value in degrees (0-360)
    /// </summary>
    public static (ImmutableDictionary<string, CssColor> Primary, ImmutableDictionary<string, CssColor> Accent) 
        GenerateFromHue(double hue)
    {
        // Normalize the hue to 0-360 range
        hue = (hue % 360 + 360) % 360;
        
        // Generate primary palette (with the exact hue)
        var primaryPalette = GeneratePaletteFromHue(hue);
        
        // Generate accent palette (30 degrees offset from primary)
        // We choose 30 degrees as it's a common complementary offset
        var accentHue = (hue - 120) % 360;
        var accentPalette = GeneratePaletteFromHue(accentHue);
        
        return (primaryPalette, accentPalette);
    }
    
    /// <summary>
    /// Generates a palette from a specific hue value
    /// </summary>
    private static ImmutableDictionary<string, CssColor> GeneratePaletteFromHue(double hue)
    {
        var palette = new Dictionary<string, CssColor>();
        
        // Generate colors for each step in the palette
        for (var i = 0; i < PaletteKeys.Length; i++)
        {
            var lightness = LightnessLevels[i] / 100.0; // Convert to 0-1 range for OKLCH
            var chroma = ChromaLevels[i];
            
            // Create the OKLCH color
            var oklchColor = $"oklch({lightness:F3} {chroma:F3} {hue:F3})";
            palette.Add(PaletteKeys[i], new CssColor(oklchColor));
        }
        
        return palette.ToImmutableDictionary();
    }
    
}
