using BlazorStatic.Models;

namespace BlazorStatic.Services;

/// <summary>
/// The BlazorStaticContentService is responsible for parsing and adding blog posts.
/// </summary>
public interface IBlazorStaticContentService
{
    /// <summary>
    ///     Gets the collection of pages that should be generated for this content.
    ///     Includes both individual post pages and tag pages.
    /// </summary>
    /// <returns>
    ///     An enumerable of PageToGenerate objects, each representing a page that
    ///     should be processed by the BlazorStaticService.
    /// </returns>
    IEnumerable<PageToGenerate> GetPagesToGenerate();
    

    /// <summary>
    ///     Gets the collection of content that should be copied to the output directory.
    ///     Typically includes media files associated with posts.
    /// </summary>
    /// <returns>
    ///     An enumerable of ContentToCopy objects, each representing a file or directory
    ///     that should be copied by the BlazorStaticService.
    /// </returns>
    IEnumerable<ContentToCopy> GetContentToCopy();
}