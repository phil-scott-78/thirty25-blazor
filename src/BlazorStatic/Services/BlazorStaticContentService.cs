using System.Collections.Immutable;

namespace BlazorStatic.Services;

using Microsoft.Extensions.Logging;

/// <summary>
///     The BlazorStaticContentService is responsible for parsing and adding blog posts.
///     It adds pages with blog posts to the options.PagesToGenerate list,
///     that is used later by BlazorStaticService to generate static pages.
/// </summary>
/// /// <typeparam name="TFrontMatter"></typeparam>
public class BlazorStaticContentService<TFrontMatter> : IContentPostService where TFrontMatter : class, IFrontMatter, new()
{
    private bool _needsRefresh = true;
    private ImmutableList<Post<TFrontMatter>> _posts = ImmutableList<Post<TFrontMatter>>.Empty;
    private readonly BlazorStaticContentOptions<TFrontMatter> _options;
    private readonly BlazorStaticHelpers _helpers;
    private readonly BlazorStaticService _blazorStaticService;
    private readonly ILogger<BlazorStaticContentService<TFrontMatter>> _logger;

    /// <summary>
    ///     The BlazorStaticContentService is responsible for parsing and adding blog posts.
    ///     It adds pages with blog posts to the options.PagesToGenerate list,
    ///     that is used later by BlazorStaticService to generate static pages.
    /// </summary>
    public BlazorStaticContentService(BlazorStaticContentOptions<TFrontMatter> options,
        BlazorStaticHelpers helpers,
        BlazorStaticService blazorStaticService,
        BlazorStaticFileWatcher blazorStaticFileWatcher,
        ILogger<BlazorStaticContentService<TFrontMatter>> logger)
    {
        _options = options;
        _helpers = helpers;
        _blazorStaticService = blazorStaticService;
        _logger = logger;

        if(_blazorStaticService.Options.HotReloadEnabled)
        {
            blazorStaticFileWatcher.Initialize([options.ContentPath], NeedsRefresh);
            HotReloadManager.Subscribe(NeedsRefresh);
        }
    }

    private void NeedsRefresh()
    {
        lock(_posts)
        {
            _needsRefresh = true;
        }
    }


    /// <summary>
    /// Place where processed blog posts live (their HTML and front matter).
    /// </summary>
    public ImmutableList<Post<TFrontMatter>> Posts
    {
        get
        {
            lock(_posts)
            {
                if(!_needsRefresh)
                {
                    return _posts;
                }

                ParseAndAddPosts();
                _needsRefresh = false;
                return _posts;
            }
        }
    }

    /// <summary>
    ///     The BlazorStaticContentOptions used to configure the BlazorStaticContentService.
    /// </summary>
    public BlazorStaticContentOptions<TFrontMatter> Options => _options;

    /// <summary>
    ///     Parses and adds posts to the BlazorStaticContentService. This method reads markdown files
    ///     from a specified directory, parses them to extract front matter and content,
    ///     and then adds them as posts to the options.PagesToGenerate.
    /// </summary>
    public void ParseAndAddPosts()
    {
        _posts = _posts.Clear();
        var (files, absPostPAth) = GetPostsPath();

        (string, string)? mediaPaths =
            _options.MediaFolderRelativeToContentPath == null || _options.MediaRequestPath == null
                ? null
                : (_options.MediaFolderRelativeToContentPath, _options.MediaRequestPath);

        foreach(var file in files)
        {
            var (htmlContent, frontMatter) = _helpers.ParseMarkdownFile<TFrontMatter>(file, mediaPaths, preProcessFile: _options.PreProcessMarkdown);

            if(frontMatter.IsDraft)
            {
                continue;
            }

            (frontMatter, htmlContent) = _options.PostProcessMarkdown(frontMatter, htmlContent);

            Post<TFrontMatter> post = new()
            {
                FrontMatter = frontMatter,
                Url = GetRelativePathWithFilename(file, absPostPAth),
                HtmlContent = htmlContent
            };

            _posts = _posts.Add(post);

            _blazorStaticService.AddPageToGenerate(new PageToGenerate($"{_options.PageUrl}/{post.Url}",
                Path.Combine(_options.PageUrl, $"{post.Url}.html"), post.FrontMatter.AdditionalInfo));
        }

        //copy media folder to output
        if(_options.MediaFolderRelativeToContentPath != null)
        {
            var pathWithMedia = Path.Combine(_options.ContentPath, _options.MediaFolderRelativeToContentPath);
            _blazorStaticService.AddContentToCopyToOutput(new ContentToCopy(pathWithMedia, pathWithMedia));
        }

        if(!typeof(IFrontMatterWithTags).IsAssignableFrom(typeof(TFrontMatter)))
        {
            if(_options.Tags.AddTagPagesFromPosts)
                _logger.LogWarning(
                    "BlazorStaticContentOptions.Tags.AddTagPagesFromPosts is true, but the used FrontMatter does not inherit from IFrontMatterWithTags. No tags were processed.");
            return;
        }

        //gather List<string> tags and create Tag objects from them.
        AllTags = _posts
            .SelectMany(post => (post.FrontMatter as IFrontMatterWithTags)?.Tags ?? Enumerable.Empty<string>())
            .Distinct()
            .Select(tag => new Tag { Name = tag, EncodedName = _options.Tags.TagEncodeFunc(tag) })
            .ToDictionary(tag => tag.Name);


        foreach(var post in _posts)
        {
            //add Tag objects to every post based on the front matter tags
            post.Tags = ((IFrontMatterWithTags)post.FrontMatter).Tags
                .Where(tagName => AllTags.ContainsKey(tagName))
                .Select(tagName => AllTags[tagName])
                .ToList();
        }

        if(!_options.Tags.AddTagPagesFromPosts) return;

        foreach(var tag in AllTags.Values)
        {
            _blazorStaticService.AddPageToGenerate(new PageToGenerate($"{_options.Tags.TagsPageUrl}/{tag.EncodedName}",
                Path.Combine(_options.Tags.TagsPageUrl, $"{tag.EncodedName}.html")));
        }

        _options.AfterContentParsedAndAddedAction?.Invoke(_blazorStaticService, this);

        return;


        (string[] Posts, string AbsContentPath) GetPostsPath() {
            //retrieves post from bin folder, where the app is running
            EnumerationOptions enumerationOptions = new()
            {
                IgnoreInaccessible = true,
                RecurseSubdirectories = true
            };

            return (Directory.GetFiles(_options.ContentPath, _options.PostFilePattern, enumerationOptions), _options.ContentPath);
        }

        //ex: file= "C:\Users\user\source\repos\MyBlog\Content\Blog\en\somePost.md"
        //returns "en/somePost"
        string GetRelativePathWithFilename(string file, string absoluteContentPath)
        {
            var relativePathWithFileName = Path.GetRelativePath(absoluteContentPath, file);
            return Path.Combine(Path.GetDirectoryName(relativePathWithFileName)!,
                    Path.GetFileNameWithoutExtension(relativePathWithFileName))
                .Replace("\\", "/");
        }
    }

    /// <summary>
    /// A dictionary of unique Tags parsed from the FrontMatter of all posts.
    /// Each Tag is distinct, and every Post references a collection of these Tag objects.
    /// </summary>
    public Dictionary<string, Tag> AllTags { get; private set; } = [];

}
