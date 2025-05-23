﻿using System.Globalization;
using System.Text;

namespace BlazorStatic;

internal static class StringExtensions
{
    /// <summary>
    /// Splits on NewLines, first normalizing line endings.
    /// </summary>
    /// <param name="s">The string to split.</param>
    /// <param name="options"></param>
    /// <returns></returns>
    public static string[] SplitNewLines(this string s, StringSplitOptions options = StringSplitOptions.None)
    {
        return s.ReplaceLineEndings(Environment.NewLine).Split(Environment.NewLine, options);
    }

    /// <summary>
    /// Converts a string to a URL-friendly slug with a single-pass algorithm.
    /// </summary>
    /// <param name="input">The string to slugify</param>
    /// <param name="maxLength">Maximum length of the resulting slug (default: 80)</param>
    /// <returns>A URL-friendly slug</returns>
    public static string Slugify(this string input, int maxLength = 80)
    {
        if (string.IsNullOrWhiteSpace(input))
            return string.Empty;

        // Normalize the string (decompose Unicode characters)
        var normalizedString = input.Normalize(NormalizationForm.FormD);

        var slugBuilder = new StringBuilder(input.Length);
        var lastWasHyphen = false;

        foreach (var c in normalizedString)
        {
            // Skip non-spacing marks (diacritics)
            if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark)
                continue;

            // Convert to lowercase and process
            var lowerChar = char.ToLowerInvariant(c);

            if (lowerChar is >= 'a' and <= 'z' or >= '0' and <= '9')
            {
                // Valid alphanumeric character
                slugBuilder.Append(lowerChar);
                lastWasHyphen = false;
            }
            else if (char.IsWhiteSpace(c) || c == '-' || c == '_' || c == '.')
            {
                // Convert whitespace, hyphens, underscores, and periods to hyphens
                // But only add a hyphen if the last character wasn't already a hyphen
                if (!lastWasHyphen && slugBuilder.Length > 0)
                {
                    slugBuilder.Append('-');
                    lastWasHyphen = true;
                }
            }
            // All other characters are ignored
        }

        // Trim hyphens from the end if needed
        var length = slugBuilder.Length;
        if (length > 0 && slugBuilder[length - 1] == '-')
            slugBuilder.Remove(length - 1, 1);

        // Enforce maximum length
        if (slugBuilder.Length > maxLength)
        {
            slugBuilder.Length = maxLength;

            // Ensure we don't end with a hyphen after truncating
            if (slugBuilder[^1] == '-')
                slugBuilder.Remove(slugBuilder.Length - 1, 1);
        }

        return slugBuilder.ToString();
    }
}