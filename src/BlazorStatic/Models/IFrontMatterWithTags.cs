namespace BlazorStatic.Models;

/// <summary>
/// If your FrontMatter uses Tags, you need to implement this interface to process the tags.
/// </summary>
public interface IFrontMatterWithTags
{
    /// <summary>
    /// Tags for the content.
    /// </summary>
    /// <remarks>
    /// If you have a different name for tags, or tags in complex objects, expose tags as a list of strings here.
    /// This is just front matter, tags will be processed with a proper encoder.
    /// </remarks>
    string[] Tags { get; init; }
}