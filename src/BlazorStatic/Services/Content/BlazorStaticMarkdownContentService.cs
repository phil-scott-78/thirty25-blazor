using System.Collections.Immutable;
using BlazorStatic.Models;
using BlazorStatic.Services.Infrastructure;

namespace BlazorStatic.Services.Content;

/// <summary>
///     Content service responsible for managing Markdown-based content in a Blazor static site.
///     This service handles parsing markdown files, extracting front matter, generating HTML, and tracking tags.
/// </summary>
/// <typeparam name="TFrontMatter">
///     The type of front matter metadata used in content files.
///     Must implement IFrontMatter and have a parameterless constructor.
/// </typeparam>
public class BlazorStaticMarkdownContentService<TFrontMatter> : IBlazorStaticContentService, IDisposable
    where TFrontMatter : class, IFrontMatter, new()
{
    private readonly ThreadSafePopulatedCache<string, MarkdownContentPage<TFrontMatter>> _contentCache;
    private readonly MarkdownContentProcessor<TFrontMatter> _contentProcessor;
    private readonly TagService<TFrontMatter> _tagService;
    private readonly ContentFilesService<TFrontMatter> _contentFilesService;
    private bool _disposed;

    /// <summary>
    ///     Initializes a new instance of the BlazorStaticContentService.
    /// </summary>
    /// <param name="options">Configuration options specific to content handling</param>
    /// <param name="fileWatcher">File watcher for hot-reload functionality</param>
    /// <param name="tagService">Service for handling tags</param>
    /// <param name="contentFilesService">Service for handling content files</param>
    /// <param name="contentProcessor">Service for processing markdown content</param>
    /// <remarks>
    ///     If hot-reload is enabled in the blazorStaticOptions, this service will watch
    ///     the content directory for changes and automatically refresh content when needed.
    /// </remarks>
    public BlazorStaticMarkdownContentService(
        BlazorStaticContentOptions<TFrontMatter> options,
        BlazorFileWatcher fileWatcher,
        TagService<TFrontMatter> tagService,
        ContentFilesService<TFrontMatter> contentFilesService,
        MarkdownContentProcessor<TFrontMatter> contentProcessor)
    {
        _tagService = tagService;
        _contentFilesService = contentFilesService;
        _contentProcessor = contentProcessor;

        // Set up the Post cache
        _contentCache =
            new ThreadSafePopulatedCache<string, MarkdownContentPage<TFrontMatter>>(async () =>
                await _contentProcessor.ProcessContentFiles());

        // Set up file watching
        fileWatcher.AddPathsWatch([
            options.ContentPath
        ], NeedsRefresh);

        HotReloadManager.Subscribe(NeedsRefresh);
    }

    /// <summary>
    ///     Marks the Posts collection as needing a refresh.
    ///     This is called when content files change and during hot-reload events.
    /// </summary>
    private void NeedsRefresh() => _contentCache.Invalidate();

    /// <summary>
    /// Gets content by its URL or returns null if not found.
    /// </summary>
    /// <param name="url">The URL identifier of the content page to retrieve</param>
    /// <returns>The content page with the matching URL, or null if no post matches the URL</returns>
    public async Task<MarkdownContentPage<TFrontMatter>?> GetContentPageByUrlOrDefault(string url)
    {
        return await _contentCache.TryGetValueAsync(url) is (true, var contentPage) ? contentPage : null;
    }

    /// <summary>
    /// Gets an immutable list of all content.
    /// </summary>
    /// <returns>An immutable list containing all parsed and processed content pages.</returns>
    public async Task<ImmutableList<MarkdownContentPage<TFrontMatter>>> GetAllContentPagesAsync()
    {
        return await _contentCache.GetValuesAsync();
    }

    /// <summary>
    /// Retrieves a tag by its encoded name along with all associated content, or returns null if the tag is not found.
    /// </summary>
    /// <param name="encodedName">The encoded name of the tag to retrieve.</param>
    /// <returns>
    ///     A task that represents the asynchronous operation.
    ///     The task result contains a tuple with the tag and its associated content pages if found, or null if no tag matches.
    /// </returns>
    public async Task<(Tag Tag, ImmutableList<MarkdownContentPage<TFrontMatter>> ContentPages)?> GetTagByEncodedNameOrDefault(
        string encodedName)
    {
        var allPosts = await GetAllContentPagesAsync();
        var contentPagesForTag = _tagService.GetPostsByTag(allPosts, encodedName);

        if (contentPagesForTag.Count == 0) return null;

        var tag = _tagService.FindTagByEncodedName(allPosts, encodedName);
        if (tag == null) return null;

        return (tag, contentPagesForTag);
    }

    /// <inheritdoc />
    async Task<ImmutableList<PageToGenerate>> IBlazorStaticContentService.GetPagesToGenerateAsync()
    {
        var allPosts = await GetAllContentPagesAsync();
        return _contentProcessor.CreatePagesToGenerate(allPosts);
    }

    /// <inheritdoc />
    Task<ImmutableList<ContentToCopy>> IBlazorStaticContentService.GetContentToCopyAsync()
    {
        return Task.FromResult(_contentFilesService.GetContentToCopy());
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