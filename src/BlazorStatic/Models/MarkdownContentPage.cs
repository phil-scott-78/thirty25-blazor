using System.Collections.Immutable;

namespace BlazorStatic.Models;

/// <summary>
/// A page generated from Markdown, along with its FrontMatter and Table of Contents.
/// </summary>
/// <typeparam name="TFrontMatter">The front matter type that must implement IFrontMatter.</typeparam>
public class MarkdownContentPage<TFrontMatter> where TFrontMatter : class, IFrontMatter
{
    /// <summary>
    ///     The metadata of the page defined in the front matter section.
    /// </summary>
    public required TFrontMatter FrontMatter { get; init; }

    /// <summary>
    ///     The URL path where the page will be published.
    ///     Derived from the file path (e.g., Content/Blog/subfolder/post-in-subfolder.md => blog/subfolder/post-in-subfolder).
    ///     Used as a route parameter, e.g., "blog/{Url}".
    /// </summary>
    public required string Url { get; init; }

    /// <summary>
    /// The URL to use for navigation. Includes the BaseUrl of the static content section.
    /// </summary>
    public required string NavigateUrl { get; init; }

    /// <summary>
    ///     The HTML content of the page, rendered from Markdown, excluding the front matter section.
    /// </summary>
    public required string HtmlContent { get; init; }

    /// <summary>
    /// The table of contents of the page, pulled from the Markdown using Header tags.
    /// </summary>
    public required TocEntry[] TableOfContents { get; init; }

    /// <summary>
    ///     The tags associated with this page.
    ///     Only populated when the TFrontMatter type implements IFrontMatterWithTags.
    /// </summary>
    public ImmutableList<Tag> Tags { get; init; } = [];
}

/// <summary>
/// A Table of Contents entry.
/// </summary>
/// <param name="Title">The title.</param>
/// <param name="Id">The id of the header.</param>
/// <param name="Children">Any children of the entry</param>
public record TocEntry(string Title, string Id, TocEntry[] Children);