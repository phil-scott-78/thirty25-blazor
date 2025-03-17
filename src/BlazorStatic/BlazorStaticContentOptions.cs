using System.Collections.Immutable;
using BlazorStatic.Models;
using BlazorStatic.Services;

namespace BlazorStatic;

internal interface IBlazorStaticContentOptions
{
    string ContentPath { get; init; }
    string? MediaFolderRelativeToContentPath { get; init; }
    string? MediaRequestPath { get; }
    string PostFilePattern { get; init; }
    string PageUrl { get; init; }
    TagsOptions Tags { get; init; }
    void CheckOptions();
}

/// <summary>
/// Options for configuring processing of md files with front matter.
/// </summary>
/// <typeparam name="TFrontMatter">Any front matter type that inherits from IFrontMatter </typeparam>
public class BlazorStaticContentOptions<TFrontMatter> : IBlazorStaticContentOptions where TFrontMatter : class, IFrontMatter, new()
{
    /// <summary>
    /// Folder relative to project root where posts are stored.
    /// Don't forget to copy the content to bin folder (use CopyToOutputDirectory in .csproj),
    /// because that's where the app will look for the files.
    /// Default is Content/Blog where posts are stored.
    /// </summary>
    public string ContentPath { get; init; } = Path.Combine("Content", "Blog");

    /// <summary>
    /// Folder in ContentPath where media files are stored.
    /// Important for app.UseStaticFiles targeting the correct folder.
    /// Null in case of no media folder.
    /// Default is "media"
    /// </summary>
    public string? MediaFolderRelativeToContentPath { get; init; } = Path.Combine( "media" );

    /// <summary>
    /// URL path for media files for posts.
    /// Used in app.UseStaticFiles to target the correct folder
    /// and in ParseAndAddPosts to generate correct URLs for images.
    /// Changes ![alt](media/image.png) to ![alt](Content/Blog/media/image.png).
    /// Leading slash / is necessary for RequestPath in app.UseStaticFiles,
    /// and is removed in ParseAndAddPosts. Null in case of no media.
    /// </summary>
    public string? MediaRequestPath  => MediaFolderRelativeToContentPath is null
        ? null
        : Path.Combine(ContentPath, MediaFolderRelativeToContentPath).Replace(@"\", "/");

    /// <summary>
    /// Pattern for blog post files in ContentPath.
    /// Default is
    /// </summary>
    public string PostFilePattern { get; init; } = "*.md";

    /// <summary>
    /// Should correspond to page that keeps the list of content.
    /// For example: @page "/blog" -> PageUrl="blog".
    /// This also serves as a generated folder name for the content.
    /// Useful for avoiding magic strings in .razor files.
    /// Default is "blog".
    /// </summary>
    public string PageUrl { get; init; } = "blog";

    /// <summary>
    /// Action to run after content is parsed and added to the collection.
    /// Useful for editing data in the posts, such as changing image paths.
    /// </summary>
    public Action<BlazorStaticService, BlazorStaticContentService<TFrontMatter>>? AfterContentParsedAndAddedAction { get; set; }

    /// <summary>
    /// Gets or sets a hook to process the markdown files before they are rendered as HTML.
    /// </summary>
    public Func<IServiceProvider, string, string> PreProcessMarkdown { get; init; } = (provider, s) => s;

    /// <summary>
    /// Gets or sets a hook to process the front matter and html after markdown parsing and before it is passed to Razor.
    /// </summary>
    public Func<IServiceProvider, TFrontMatter, string, (TFrontMatter, string)> PostProcessMarkdown { get; init; } = (provider, frontMatter, html) => (frontMatter, html);

    /// <summary>
    /// Gets a list of excluded mapped routes from static content generation.
    /// </summary>
    public ImmutableList<string> ExcludeMapRoutes { get; init; } = [];

    /// <summary>
    /// Validates the configuration properties to ensure required fields are set correctly.
    /// This validation is run when registering the service.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown if <see cref="ContentPath"/> or <see cref="PageUrl"/> are null or empty.
    /// </exception>
    public void CheckOptions()
    {
        if (string.IsNullOrWhiteSpace(ContentPath))
            throw new InvalidOperationException("ContentPath must be set and cannot be null or empty.");

        if (string.IsNullOrWhiteSpace(PageUrl))
            throw new InvalidOperationException("PageUrl must be set and cannot be null or empty.");
    }

    /// <summary>
    /// Options related to tags
    /// </summary>
    public TagsOptions Tags { get; init; } = new();
}

/// <summary>
/// Options related to tags
/// </summary>
public class TagsOptions
{
    /// <summary>
    ///     tag pages will be generated from all tags found in blog posts
    /// </summary>
    public bool AddTagPagesFromPosts { get; set; } = true;
    /// <summary>
    ///     Should correspond to @page "/tags" (here in relative path: "tags")
    ///     Useful for avoiding magic strings in .razor files
    /// </summary>
    public string TagsPageUrl { get; set; } = "tags";

    /// <summary>
    /// Func to convert tag string to file-name/url.
    /// Also don't forget to use the same encoder while creating tag links
    /// </summary>
    public Func<string, string> TagEncodeFunc { get; set; } = s => s.Slugify();

}
