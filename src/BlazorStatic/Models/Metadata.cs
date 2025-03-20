namespace BlazorStatic.Models;

/// <summary>
///     Additional AdditionalInfo related to the page. This info is typically not bounded to FrontMatter, but rather
///     "computed" additionaly.
///     Currently, it is used to pass LastMod to the node in xml sitemap
/// </summary>
public class Metadata
{
    /// <summary>
    /// The date when the page was last modified
    /// </summary>
    public DateTime? LastMod { get; set; }

    /// <summary>
    /// The title of the page, used for RSS feed items
    /// </summary>
    public string? Title { get; set; }

    /// <summary>
    /// The description of the page, used for RSS feed items
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Indicates whether this page should be included in the RSS feed
    /// </summary>
    public bool RssItem { get; set; } = true;
}