using BlazorStatic.Models;
using BlazorStatic.Services.Infrastructure;
using Microsoft.Extensions.Logging;
using System.Collections.Immutable;

namespace BlazorStatic.Services.Content;

/// <summary>
/// Service for handling content file operations in a BlazorStatic site.
/// </summary>
/// <typeparam name="TFrontMatter">The type of front matter used in content.</typeparam>
public class ContentFilesService<TFrontMatter>
    where TFrontMatter : class, IFrontMatter
{
    private readonly BlazorStaticContentOptions<TFrontMatter> _options;
    private readonly ILogger<ContentFilesService<TFrontMatter>> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ContentFilesService{TFrontMatter}"/> class.
    /// </summary>
    /// <param name="options">Content options.</param>
    /// <param name="logger">Logger instance.</param>
    public ContentFilesService(
        BlazorStaticContentOptions<TFrontMatter> options,
        ILogger<ContentFilesService<TFrontMatter>> logger)
    {
        _options = options;
        _logger = logger;
    }

    /// <summary>
    /// Gets all content files that match the configured pattern.
    /// </summary>
    /// <returns>A tuple with the array of file paths and the absolute content path.</returns>
    /// <exception cref="FileOperationException">Thrown when there is an error accessing content files.</exception>
    internal (string[] Files, string AbsContentPath) GetContentFiles()
    {
        try
        {
            // Validate the content path exists
            var absoluteContentPath = PathUtilities.ValidateDirectoryPath(_options.ContentPath);

            return PathUtilities.GetFilesInDirectory(
                absoluteContentPath,
                _options.PostFilePattern);
        }
        catch (DirectoryNotFoundException ex)
        {
            _logger.LogError(ex, "Content directory not found: {ContentPath}", _options.ContentPath);
            throw new FileOperationException(
                "Content directory not found",
                _options.ContentPath,
                ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting content files from {ContentPath}", _options.ContentPath);
            throw new FileOperationException(
                "Failed to retrieve content files",
                _options.ContentPath,
                ex);
        }
    }

    /// <summary>
    /// Creates a content URL from a file path.
    /// </summary>
    /// <param name="filePath">The full file path.</param>
    /// <param name="baseContentPath">The base content path.</param>
    /// <returns>A URL for the content.</returns>
    internal string CreateContentUrl(string filePath, string baseContentPath)
    {
        ArgumentException.ThrowIfNullOrEmpty(filePath);
        ArgumentException.ThrowIfNullOrEmpty(baseContentPath);

        try
        {
            return PathUtilities.FilePathToUrlPath(filePath, baseContentPath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create content URL for {FilePath}", filePath);
            throw new FileOperationException(
                "Failed to create content URL",
                filePath,
                ex);
        }
    }

    /// <summary>
    /// Creates a navigation URL from a content URL.
    /// </summary>
    /// <param name="contentUrl">The content URL.</param>
    /// <returns>A full navigation URL.</returns>
    internal string CreateNavigationUrl(string contentUrl)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(contentUrl);

        return PathUtilities.CombineUrl(_options.PageUrl, contentUrl);
    }

    /// <summary>
    /// Gets the output file path for a content URL.
    /// </summary>
    /// <param name="contentUrl">The content URL.</param>
    /// <returns>An output file path.</returns>
    internal string GetOutputFilePath(string contentUrl)
    {
        ArgumentException.ThrowIfNullOrEmpty(contentUrl);

        var relativePath = contentUrl.Replace('/', Path.DirectorySeparatorChar);
        return Path.Combine(_options.PageUrl, $"{relativePath}.html");
    }

    /// <summary>
    /// Gets the page URL for a content URL.
    /// </summary>
    /// <param name="contentUrl">The content URL.</param>
    /// <returns>A page URL.</returns>
    internal string GetPageUrl(string contentUrl)
    {
        ArgumentException.ThrowIfNullOrEmpty(contentUrl);

        return PathUtilities.CombineUrl(_options.PageUrl, contentUrl);
    }

    /// <summary>
    /// Gets the content to be copied to the output directory.
    /// </summary>
    /// <returns>A list of content to copy.</returns>
    internal ImmutableList<ContentToCopy> GetContentToCopy()
    {
        try
        {
            // Validate the content path exists
            PathUtilities.ValidateDirectoryPath(_options.ContentPath);

            return new[]
            {
                new ContentToCopy(_options.ContentPath, _options.PageUrl)
            }.ToImmutableList();
        }
        catch (DirectoryNotFoundException ex)
        {
            _logger.LogWarning(ex, "Content directory not found: {ContentPath}", _options.ContentPath);
            return ImmutableList<ContentToCopy>.Empty;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting content to copy from {ContentPath}", _options.ContentPath);
            return ImmutableList<ContentToCopy>.Empty;
        }
    }
}