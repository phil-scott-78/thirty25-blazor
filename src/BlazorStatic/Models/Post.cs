using System.Collections.Immutable;

namespace BlazorStatic.Models;

/// <summary>
///     Represents a blog post with metadata and HTML content parsed from Markdown.
/// </summary>
/// <typeparam name="TFrontMatter">The front matter type that must implement IFrontMatter.</typeparam>
public class Post<TFrontMatter> where TFrontMatter : class, IFrontMatter, new()
{
    /// <summary>
    ///     The metadata of the post defined in the front matter section.
    /// </summary>
    public required TFrontMatter FrontMatter { get; init; }

    /// <summary>
    ///     The URL path where the post will be published.
    ///     Derived from the file path (e.g., Content/Blog/subfolder/post-in-subfolder.md => blog/subfolder/post-in-subfolder).
    ///     Used as route parameter, e.g., "blog/{Url}".
    /// </summary>
    public required string Url { get; init; }
    
    /// <summary>
    /// The URL to use for navigation. Includes the BaseUrl of the static content section.
    /// </summary>
    public required string NavigateUrl { get; init; }

    /// <summary>
    ///     The HTML content of the post, rendered from Markdown, excluding the front matter section.
    /// </summary>
    public required string HtmlContent { get; init; }

    /// <summary>
    ///     The tags associated with this post.
    ///     Only populated when the TFrontMatter type implements IFrontMatterWithTags.
    /// </summary>
    public ImmutableList<Tag> Tags { get; init; } = [];
}