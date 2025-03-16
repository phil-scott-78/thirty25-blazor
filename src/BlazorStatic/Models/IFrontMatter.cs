namespace BlazorStatic.Models;

/// <summary>
///     Interface for front matter. FrontMatter is the metadata of a post.
/// </summary>
public interface IFrontMatter
{
    /// <summary>
    ///     If true, the blog post will not be generated.
    /// </summary>
    bool IsDraft => false;

    /// <summary>
    /// Converts the FrontMatter into structured metadata for RSS and SiteMap generation
    /// </summary>
    /// <returns></returns>
    public Metadata AsMetadata();
}