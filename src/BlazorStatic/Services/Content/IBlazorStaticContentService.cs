using System.Collections.Immutable;
using BlazorStatic.Models;

namespace BlazorStatic.Services.Content;

/// <summary>
/// The BlazorStaticContentService is responsible for parsing and handling content.
/// </summary>
internal interface IBlazorStaticContentService
{
    /// <summary>
    /// Gets the collection of pages that should be generated for this content.
    /// </summary>
    /// <returns>
    /// An ImmutableList of PageToGenerate objects, each representing a page that
    /// should be processed by the BlazorStaticService.
    /// </returns>
    Task<ImmutableList<PageToGenerate>> GetPagesToGenerateAsync();


    /// <summary>
    /// Gets the collection of content that should be copied to the output directory.
    /// </summary>
    /// <returns>
    /// An ImmutableList of ContentToCopy objects, each representing a file or directory
    /// that should be copied by the BlazorStaticService.
    /// </returns>
    Task<ImmutableList<ContentToCopy>> GetContentToCopyAsync();
}