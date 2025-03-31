using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Diagnostics;
using System.IO.Abstractions;
using BlazorStatic.Models;

namespace BlazorStatic.Services;

using Microsoft.Extensions.Logging;

/// <summary>
///     Content service responsible for managing blog posts and other content in a Blazor static site.
///     This service handles parsing markdown files, extracting front matter, generating HTML, and tracking tags.
/// </summary>
/// <typeparam name="TFrontMatter">
///     The type of front matter metadata used in content files.
///     Must implement IFrontMatter and have a parameterless constructor.
/// </typeparam>
public class BlazorStaticMarkdownContentService<TFrontMatter> : IBlazorStaticContentService, IDisposable
    where TFrontMatter : class, IFrontMatter, new()
{
    private readonly BlazorStaticContentOptions<TFrontMatter> _options;
    private readonly ThreadSafePopulatedCache<string, Post<TFrontMatter>> _postCache;
    private readonly MarkdownService _markdownService;
    private readonly IFileSystem _fileSystem;
    private readonly ILogger<BlazorStaticMarkdownContentService<TFrontMatter>> _logger;
    private bool _disposed;

    /// <summary>
    ///     Initializes a new instance of the BlazorStaticContentService.
    /// </summary>
    /// <param name="options">Configuration options specific to content handling</param>
    /// <param name="blazorStaticFileWatcher">File watcher for hot-reload functionality</param>
    /// <param name="markdownService">Service used to parse and render markdown files</param>
    /// <param name="fileSystem">The file system</param>
    /// <param name="logger">Logger for diagnostic information</param>
    /// <remarks>
    ///     If hot-reload is enabled in the blazorStaticOptions, this service will watch
    ///     the content directory for changes and automatically refresh posts when needed.
    /// </remarks>
    public BlazorStaticMarkdownContentService(BlazorStaticContentOptions<TFrontMatter> options,
        BlazorStaticFileWatcher blazorStaticFileWatcher,
        MarkdownService markdownService,
        IFileSystem fileSystem,
        ILogger<BlazorStaticMarkdownContentService<TFrontMatter>> logger)
    {
        _options = options;
        _markdownService = markdownService;
        _fileSystem = fileSystem;
        _logger = logger;
        _postCache = new ThreadSafePopulatedCache<string, Post<TFrontMatter>>(async () => await ParseAndAddPosts());

        blazorStaticFileWatcher.Initialize([options.ContentPath], NeedsRefresh);
        HotReloadManager.Subscribe(NeedsRefresh);
    }

    /// <summary>
    ///     Marks the posts collection as needing a refresh.
    ///     This is called when content files change and during hot-reload events.
    /// </summary>
    private void NeedsRefresh() => _postCache.Invalidate();

    /// <summary>
    ///     Gets a post by its URL, or returns null if not found.
    /// </summary>
    /// <param name="url">The URL identifier of the post to retrieve</param>
    /// <returns>The post with the matching URL, or null if no post matches the URL</returns>
    /// <remarks>
    ///     This method uses the backing <see cref="ThreadSafePopulatedCache{TKey,TValue}"/> which 
    ///     locks and repopulates its contents when accessed after invalidation. If <see cref="NeedsRefresh"/>
    ///     has been called, the next access will trigger thread-safe repopulation of all posts.
    /// </remarks>
    public async Task<Post<TFrontMatter>?> GetPostByUrlOrDefault(string url)
    {
        return await _postCache.TryGetValueAsync(url) is (true, var post) ? post : null;
    }

    /// <summary>
    ///     Gets an immutable list of all posts.
    /// </summary>
    /// <returns>An immutable list containing all parsed and processed posts</returns>
    /// <remarks>
    ///     Accessing this method will ensure the posts cache is initialized. If <see cref="NeedsRefresh"/> 
    ///     has been called to invalidate the cache, this method will trigger a thread-safe repopulation
    ///     of all posts by acquiring a write lock, clearing existing data, and reprocessing all markdown files.
    /// </remarks>
    public async Task<ImmutableList<Post<TFrontMatter>>> GetAllPostsAsync()
    {
        return await _postCache.GetValuesAsync();
    }

    /// <summary>
    ///     Retrieves a tag by its encoded name along with all associated posts, or returns null if the tag is not found.
    /// </summary>
    /// <param name="encodedName">The encoded name of the tag to retrieve.</param>
    /// <returns>
    ///     A task that represents the asynchronous operation.
    ///     The task result contains a tuple with the tag and its associated posts if found, or null if no tag matches.
    /// </returns>
    /// <remarks>
    ///     This method retrieves all posts from the post cache, which may trigger a thread-safe repopulation
    ///     if the cache has been invalidated via <see cref="NeedsRefresh"/>. During repopulation,
    ///     the backing <see cref="ThreadSafePopulatedCache{TKey,TValue}"/> acquires a write lock,
    ///     clears existing data, and rebuilds all posts and their associated tags before filtering and returning results.
    ///     The method returns both the tag object and an immutable list of all posts that contain this tag.
    /// </remarks>
    public async Task<(Tag Tag, ImmutableList<Post<TFrontMatter>> Posts)?> GetTagByEncodedNameOrDefault(
        string encodedName)
    {
        var allPosts = await GetAllPostsAsync();
        var postsForTag = allPosts
            .Where(x => x.Tags.Any(c => c.EncodedName == encodedName))
            .ToImmutableList();

        if (postsForTag.Count == 0) return null;

        var tag = postsForTag[0].Tags.First(i => i.EncodedName == encodedName);

        return (tag, postsForTag);
    }


    /// <inheritdoc />
    async Task<ImmutableList<PageToGenerate>> IBlazorStaticContentService.GetPagesToGenerateAsync()
    {
        var pageToGenerates = ImmutableList<PageToGenerate>.Empty;

        // Post pages - one for each blog post
        var allPosts = await GetAllPostsAsync();

        // Generate pages for each blog post
        foreach (var post in allPosts)
        {
            var relativePath = post.Url.Replace('/', Path.DirectorySeparatorChar);
            var outputFile = _fileSystem.Path.Combine(_options.PageUrl, $"{relativePath}.html");
            var pageUrl = $"{_options.PageUrl}/{post.Url}";

            pageToGenerates = pageToGenerates.Add(new PageToGenerate(pageUrl, outputFile, post.FrontMatter.AsMetadata()));
        }

        // Extract all unique tags from posts
        var allTags = allPosts
            .SelectMany(post => post.Tags)
            .DistinctBy(tag => tag.EncodedName);

        // Generate tag pages - one for each unique tag
        foreach (var tag in allTags)
        {
            var outputFile = _fileSystem.Path.Combine(_options.Tags.TagsPageUrl, $"{tag.EncodedName}.html");
            var pageUrl = $"{_options.Tags.TagsPageUrl}/{tag.EncodedName}";

            pageToGenerates = pageToGenerates.Add(new PageToGenerate(pageUrl, outputFile));
        }

        return pageToGenerates;
    }

    /// <inheritdoc />
    Task<ImmutableList<ContentToCopy>> IBlazorStaticContentService.GetContentToCopyAsync()
    {
        return Task.FromResult(new []
        {
            new ContentToCopy(_options.ContentPath, _options.PageUrl)
        }.ToImmutableList());
    }

    private async Task<IEnumerable<KeyValuePair<string, Post<TFrontMatter>>>> ParseAndAddPosts()
    {
        var stopwatch = Stopwatch.StartNew();

        var (files, absPostPath) = GetPostsPath();
        var results = new ConcurrentDictionary<string, Post<TFrontMatter>>();

        await Parallel.ForEachAsync(files, async (file, _) =>
        {
            // Parse markdown and extract front matter
            var (frontMatter, htmlContent) = await _markdownService.ParseMarkdownFileAsync(
                file,
                _options.ContentPath,
                _options.PageUrl,
                preProcessFile: _options.PreProcessMarkdown,
                postProcessHtml: _options.PostProcessHtml
            );

            // Skip draft posts
            if (frontMatter.IsDraft)
            {
                return; // 'continue' in a parallel loop becomes 'return'
            }

            // Process tags if supported
            var tags = frontMatter is IFrontMatterWithTags frontMatterWithTags
                ? frontMatterWithTags.Tags.Select(BuildTag).ToImmutableList()
                : [];

            // Create the post with all required information
            Post<TFrontMatter> post = new()
            {
                FrontMatter = frontMatter,
                Url = GetRelativePathWithFilename(file, absPostPath),
                NavigateUrl = $"{_options.PageUrl}/{GetRelativePathWithFilename(file, absPostPath)}",
                HtmlContent = htmlContent,
                Tags = tags
            };

            // Add to concurrent dictionary instead of yield returning
            results.TryAdd(post.Url, post);
        });

        stopwatch.Stop();
        _logger.LogInformation("Posts and tagged rebuilt in {elapsed}", stopwatch.Elapsed);

        return results;
    }

    private Tag BuildTag(string tagName)
    {
        var tagEncodedName = _options.Tags.TagEncodeFunc(tagName);
        return new Tag
        {
            Name = tagName,
            EncodedName = tagEncodedName,
            NavigateUrl = _options.Tags.TagsPageUrl + "/" + tagEncodedName,
        };
    }

    private string GetRelativePathWithFilename(string file, string absoluteContentPath)
    {
        var fileSystemPath = _fileSystem.Path;
        var relativePathWithFileName = fileSystemPath.GetRelativePath(absoluteContentPath, file);
        return fileSystemPath
            .Combine(fileSystemPath.GetDirectoryName(relativePathWithFileName)!, fileSystemPath.GetFileNameWithoutExtension(relativePathWithFileName).Slugify())
            .Replace("\\", "/");
    }

    private (string[] Posts, string AbsContentPath) GetPostsPath()
    {
        // Configure enumeration options for directory search
        EnumerationOptions enumerationOptions = new()
        {
            IgnoreInaccessible = true,
            RecurseSubdirectories = true
        };

        // Get all files matching the pattern and return with the content path
        return (_fileSystem.Directory.GetFiles(_options.ContentPath, _options.PostFilePattern, enumerationOptions),
            _options.ContentPath);
    }


    /// <inheritdoc />
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    private void Dispose(bool disposing)
    {
        if (_disposed) return;
        if (disposing)
        {
            _markdownService.Dispose();
            HotReloadManager.Unsubscribe(NeedsRefresh);
        }

        _disposed = true;
    }

    /// <summary>
    /// Finalizer for the <see cref="BlazorStaticMarkdownContentService{TFrontMatter}"/> class.
    /// </summary>
    ~BlazorStaticMarkdownContentService()
    {
        Dispose(false);
    }
}