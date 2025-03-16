using System.Collections.Immutable;
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
public class BlazorStaticContentService<TFrontMatter> : IBlazorStaticContentService
    where TFrontMatter : class, IFrontMatter, new()
{
    private bool _needsRefresh = true;
    private ImmutableList<Post<TFrontMatter>> _posts = ImmutableList<Post<TFrontMatter>>.Empty;
    private readonly MarkdownService _markdownService;
    private readonly ILogger<BlazorStaticContentService<TFrontMatter>> _logger;
    private readonly Lock _postPostsLock = new Lock();

    /// <summary>
    ///     Initializes a new instance of the BlazorStaticContentService.
    /// </summary>
    /// <param name="options">Configuration options specific to content handling</param>
    /// <param name="blazorStaticOptions">General BlazorStatic configuration options</param>
    /// <param name="blazorStaticFileWatcher">File watcher for hot-reload functionality</param>
    /// <param name="markdownService">Service used to parse and render markdown files</param>
    /// <param name="logger">Logger for diagnostic information</param>
    /// <remarks>
    ///     If hot-reload is enabled in the blazorStaticOptions, this service will watch
    ///     the content directory for changes and automatically refresh posts when needed.
    /// </remarks>
    public BlazorStaticContentService(BlazorStaticContentOptions<TFrontMatter> options,
        BlazorStaticOptions blazorStaticOptions,
        BlazorStaticFileWatcher blazorStaticFileWatcher,
        MarkdownService markdownService,
        ILogger<BlazorStaticContentService<TFrontMatter>> logger)
    {
        Options = options;
        _markdownService = markdownService;
        _logger = logger;

        if (blazorStaticOptions.HotReloadEnabled)
        {
            blazorStaticFileWatcher.Initialize([options.ContentPath], NeedsRefresh);
            HotReloadManager.Subscribe(NeedsRefresh);
        }
    }

    /// <summary>
    ///     Marks the posts collection as needing a refresh.
    ///     This is called when content files change and during hot-reload events.
    /// </summary>
    private void NeedsRefresh()
    {
        lock (_postPostsLock)
        {
            _needsRefresh = true;
        }
    }

    /// <summary>
    ///     Gets the collection of processed blog posts with their HTML content and front matter.
    ///     Automatically refreshes the collection if content files have changed.
    /// </summary>
    /// <remarks>
    ///     This property is thread-safe and will only refresh the posts when necessary.
    ///     The collection is stored as an immutable list to prevent unintended modifications.
    /// </remarks>
    public ImmutableList<Post<TFrontMatter>> Posts
    {
        get
        {
            lock (_postPostsLock)
            {
                if (!_needsRefresh)
                {
                    return _posts;
                }

                _posts = ParseAndAddPosts();
                _needsRefresh = false;
                return _posts;
            }
        }
    }

    /// <summary>
    ///     A dictionary of all unique tags found across all posts.
    ///     The key is the tag name, and the value is a Tag object with both the original
    ///     name and an encoded version suitable for use in URLs.
    /// </summary>
    /// <remarks>
    ///     This dictionary ensures that each tag has exactly one Tag object instance,
    ///     which is referenced by all posts that use that tag.
    /// </remarks>
    public ImmutableDictionary<string, Tag> AllTags => Posts
            .SelectMany(post => post.Tags)
            .DistinctBy(tag => tag.Name)
            .ToImmutableDictionary(tag => tag.Name);

    /// <summary>
    ///     Gets the configuration options used by this content service.
    /// </summary>
    public BlazorStaticContentOptions<TFrontMatter> Options { get; }


    /// <inheritdoc />
    public IEnumerable<PageToGenerate> GetPagesToGenerate()
    {
        // Post pages - one for each blog post
        foreach (var post in Posts)
        {
            yield return new PageToGenerate(
                $"{Options.PageUrl}/{post.Url}",
                Path.Combine(Options.PageUrl, $"{post.Url}.html"), post.FrontMatter.AsMetadata());
        }

        // Tag pages - one page for each unique tag
        foreach (var tag in AllTags.Values)
        {
            yield return new PageToGenerate(
                $"{Options.Tags.TagsPageUrl}/{tag.EncodedName}",
                Path.Combine(Options.Tags.TagsPageUrl, $"{tag.EncodedName}.html"));
        }
    }

    /// <inheritdoc />
    public IEnumerable<ContentToCopy> GetContentToCopy()
    {
        if (Options.MediaFolderRelativeToContentPath == null)
        {
            yield break;
        }

        var pathWithMedia = Path.Combine(Options.ContentPath, Options.MediaFolderRelativeToContentPath);
        yield return new ContentToCopy(pathWithMedia, pathWithMedia);
    }

    /// <summary>
    ///     Parses markdown files and creates Post objects for each valid file found.
    ///     Extracts front matter, renders HTML content, and processes tags.
    /// </summary>
    /// <returns>
    ///     An immutable list of Post objects representing all non-draft posts.
    /// </returns>
    /// <remarks>
    ///     This method handles several key operations:
    ///     1. Finds all markdown files in the configured content path
    ///     2. Parses each file to extract front matter and convert content to HTML
    ///     3. Skips draft posts based on the IsDraft property of the front matter
    ///     4. Processes tags if the front matter implements IFrontMatterWithTags
    ///     5. Builds a collection of Post objects with all required information
    ///     6. Updates the AllTags dictionary with unique tags
    /// </remarks>
    private ImmutableList<Post<TFrontMatter>> ParseAndAddPosts()
    {
        var posts = new List<Post<TFrontMatter>>();
        var (files, absPostPath) = GetPostsPath();

        // Configure media paths if both source and request paths are provided
        (string, string)? mediaPaths =
            Options is { MediaFolderRelativeToContentPath: not null, MediaRequestPath: not null }
                ? (Options.MediaFolderRelativeToContentPath, Options.MediaRequestPath)
                : null;

        // Determine if front matter supports tags
        var supportsTags = typeof(IFrontMatterWithTags).IsAssignableFrom(typeof(TFrontMatter));

        // Process each markdown file
        foreach (var file in files)
        {
            // Parse markdown and extract front matter
            var (frontMatter, htmlContent) = _markdownService.ParseMarkdownFile<TFrontMatter>(file, mediaPaths, preProcessFile: Options.PreProcessMarkdown);

            // Skip draft posts
            if (frontMatter.IsDraft)
            {
                continue;
            }

            // Apply any configured post-processing
            (frontMatter, htmlContent) = Options.PostProcessMarkdown(frontMatter, htmlContent);

            // Process tags if supported
            var tags = supportsTags && frontMatter is IFrontMatterWithTags frontMatterWithTags
                ? frontMatterWithTags.Tags.Select(BuildTag).ToImmutableList()
                : [];

            // Create the post with all required information
            Post<TFrontMatter> post = new()
            {
                FrontMatter = frontMatter,
                Url = GetRelativePathWithFilename(file, absPostPath),
                NavigateUrl = $"{Options.PageUrl}/{GetRelativePathWithFilename(file, absPostPath)}",
                HtmlContent = htmlContent,
                Tags = tags
            };
            posts.Add(post);
        }

        // Log warning if tag processing was expected but not possible
        if (!supportsTags && Options.Tags.AddTagPagesFromPosts)
        {
            _logger.LogWarning(
                "BlazorStaticContentOptions.Tags.AddTagPagesFromPosts is true, but the used FrontMatter does not inherit from IFrontMatterWithTags. No tags were processed.");
        }

        return posts.ToImmutableList();
    }

    private Tag BuildTag(string tagName)
    {
        var tagEncodedName = Options.Tags.TagEncodeFunc(tagName);
        return new Tag
        {
            Name = tagName,
            EncodedName = tagEncodedName,
            NavigateUrl = Options.Tags.TagsPageUrl + "/" + tagEncodedName,
        };
    }

    private static string GetRelativePathWithFilename(string file, string absoluteContentPath)
    {
        var relativePathWithFileName = Path.GetRelativePath(absoluteContentPath, file);
        return Path.Combine(Path.GetDirectoryName(relativePathWithFileName)!,
                Path.GetFileNameWithoutExtension(relativePathWithFileName))
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
        return (Directory.GetFiles(Options.ContentPath, Options.PostFilePattern, enumerationOptions),
            Options.ContentPath);
    }
}