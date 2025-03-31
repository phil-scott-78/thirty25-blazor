namespace BlazorStatic.Models;

/// <summary>
/// If your FrontMatter uses Tags you need to implement this interface to process the tags.
/// </summary>
public interface IFrontMatterWithTags
{
    /// <summary>
    ///     Tags for the post.
    ///     If you have a different name for tags, or tags in complex objects, expose tags as a list of strings here.
    /// This is just front matter, tags will be process with proper encoder.
    /// </summary>
    string[] Tags { get; set; }
}