using System.Collections.Immutable;
using System.Diagnostics;
using BlazorStatic.Models;
using BlazorStatic.Services.Content.MarkdigExtensions;
using Microsoft.Extensions.Logging;

namespace BlazorStatic.Services.Content;

/// <summary>
/// Processes markdown content into HTML with front matter extraction.
/// </summary>
/// <typeparam name="TFrontMatter">The type of front matter used in content.</typeparam>
public class MarkdownContentProcessor<TFrontMatter>
    where TFrontMatter : class, IFrontMatter, new()
{
    private readonly BlazorStaticContentOptions<TFrontMatter> _options;
    private readonly MarkdownParserService _markdownParserService;
    private readonly TagService<TFrontMatter> _tagService;
    private readonly ContentFilesService<TFrontMatter> _contentFilesService;
    private readonly ILogger<MarkdownContentProcessor<TFrontMatter>> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="MarkdownContentProcessor{TFrontMatter}"/> class.
    /// </summary>
    /// <param name="options">Content options.</param>
    /// <param name="markdownParserService">Markdown service for parsing and rendering.</param>
    /// <param name="tagService">Tag service for handling tags.</param>
    /// <param name="contentFilesService">Content files service for file operations.</param>
    /// <param name="logger">Logger instance.</param>
    public MarkdownContentProcessor(
        BlazorStaticContentOptions<TFrontMatter> options,
        MarkdownParserService markdownParserService,
        TagService<TFrontMatter> tagService,
        ContentFilesService<TFrontMatter> contentFilesService,
        ILogger<MarkdownContentProcessor<TFrontMatter>> logger)
    {
        _options = options;
        _markdownParserService = markdownParserService;
        _tagService = tagService;
        _contentFilesService = contentFilesService;
        _logger = logger;
    }

    /// <summary>
    /// Processes all content files and creates the Post objects.
    /// </summary>
    /// <returns>A dictionary of URL to Post objects.</returns>
    internal async Task<Dictionary<string, MarkdownContentPage<TFrontMatter>>> ProcessContentFiles()
    {
        var stopwatch = Stopwatch.StartNew();
        var results = new Dictionary<string, MarkdownContentPage<TFrontMatter>>();

        try
        {
            var (files, absContentPath) = _contentFilesService.GetContentFiles();

            if (files.Length == 0)
            {
                _logger.LogWarning("No content files found in {ContentPath}", absContentPath);
                return results;
            }

            _logger.LogInformation("Processing {Count} content files from {ContentPath}", files.Length, absContentPath);

            foreach (var file in files)
            {
                try
                {
                    var contentPage = await ProcessContentFile(file, absContentPath);

                    if (contentPage != null)
                    {
                        results[contentPage.Url] = contentPage;
                    }
                }
                catch (ContentProcessingException ex)
                {
                    // Log the error and continue with the next file
                    _logger.LogError(ex, "Error processing file: {FilePath}", file);
                }
                catch (Exception ex)
                {
                    // Wrap unexpected exceptions in a ContentProcessingException and continue
                    _logger.LogError(ex, "Unexpected error processing file: {FilePath}", file);
                }
            }

            stopwatch.Stop();
            _logger.LogInformation("Processed {Count} content files in {Elapsed}", results.Count, stopwatch.Elapsed);

            return results;
        }
        catch (FileOperationException ex)
        {
            // This exception is already logged in ContentFilesService
            stopwatch.Stop();
            _logger.LogWarning("File operation error in {Elapsed}: {Message}", stopwatch.Elapsed, ex.Message);
            return results;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "Error processing content files in {Elapsed}", stopwatch.Elapsed);
            return results;
        }
    }

    /// <summary>
    /// Processes a single content file.
    /// </summary>
    /// <param name="filePath">The file to process.</param>
    /// <param name="baseContentPath">The base content path.</param>
    /// <returns>A <see cref="MarkdownContentPage{TFrontMatter}"/> object if successful, null otherwise.</returns>
    /// <exception cref="ContentProcessingException">Thrown when there is an error processing the content.</exception>
    private async Task<MarkdownContentPage<TFrontMatter>?> ProcessContentFile(string filePath, string baseContentPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(baseContentPath);

        try
        {
            _logger.LogDebug("Processing {FilePath} markdown", filePath);

            if (!File.Exists(filePath))
            {
                throw new FileOperationException("File does not exist", filePath);
            }

            // Parse Markdown and extract front matter
            var (frontMatter, markdownContent, toc) = await _markdownParserService.ParseMarkdownFileAsync<TFrontMatter>(
                filePath,
                _options.ContentPath,
                _options.PageUrl,
                preProcessFile: _options.PreProcessMarkdown
            );

            // Skip draft content pages
            if (frontMatter.IsDraft)
            {
                _logger.LogDebug("Skipping draft content page: {FilePath}", filePath);
                return null;
            }

            // Process tags
            var tags = _tagService.ExtractTagsFromFrontMatter(frontMatter);

            // Create the content URL
            var contentUrl = _contentFilesService.CreateContentUrl(filePath, baseContentPath);

            // Create the content page with all required information
            return new MarkdownContentPage<TFrontMatter>
            {
                FrontMatter = frontMatter,
                Url = contentUrl,
                NavigateUrl = _contentFilesService.CreateNavigationUrl(contentUrl),
                MarkdownContent = markdownContent,
                Tags = tags,
                TableOfContents = toc
            };
        }
        catch (FileOperationException)
        {
            // Re-throw FileOperationExceptions as they are already properly formatted
            throw;
        }
        catch (Exception ex)
        {
            // Wrap any other exceptions in a ContentProcessingException
            throw new ContentProcessingException("Error processing markdown file", filePath, ex);
        }
    }

    /// <summary>
    /// Creates pages to generate for a collection of content pages.
    /// </summary>
    /// <param name="contentPages">The posts to create pages for.</param>
    /// <returns>An immutable list of pages to generate.</returns>
    internal ImmutableList<PageToGenerate> CreatePagesToGenerate(
        IEnumerable<MarkdownContentPage<TFrontMatter>> contentPages)
    {
        ArgumentNullException.ThrowIfNull(contentPages);

        try
        {
            var pageToGenerates = ImmutableList<PageToGenerate>.Empty;
            var allPosts = contentPages.ToList();

            if (allPosts.Count == 0)
            {
                _logger.LogWarning("No posts available to generate pages from");
                return pageToGenerates;
            }

            // Generate pages for each blog post
            foreach (var post in allPosts)
            {
                var outputFile = _contentFilesService.GetOutputFilePath(post.Url);
                var pageUrl = _contentFilesService.GetPageUrl(post.Url);

                pageToGenerates = pageToGenerates.Add(
                    new PageToGenerate(pageUrl, outputFile, post.FrontMatter.AsMetadata()));
            }

            // Extract all unique tags from posts
            var allTags = _tagService.GetUniqueTagsFromContentPages(allPosts).ToList();

            // Generate tag pages - one for each unique tag
            foreach (var tag in allTags)
            {
                var outputFile = Path.Combine(_options.Tags.TagsPageUrl, $"{tag.EncodedName}.html");
                var pageUrl = $"{_options.Tags.TagsPageUrl}/{tag.EncodedName}";

                pageToGenerates = pageToGenerates.Add(new PageToGenerate(pageUrl, outputFile));
            }

            return pageToGenerates;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating pages to generate");
            // Return an empty list rather than throwing to avoid breaking the generation process
            return ImmutableList<PageToGenerate>.Empty;
        }
    }
}