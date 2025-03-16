using System.Collections.Immutable;
using Markdig;
using BlazorStatic.Models;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace BlazorStatic;

/// <summary>
///     Configuration options for the BlazorStatic static site generation process.
/// </summary>
public class BlazorStaticOptions
{
    /// <summary>
    /// The title of the blog. 
    /// </summary>
    public required string BlogTitle { get; init; }

    /// <summary>
    /// The leading or description of the blog.
    /// </summary>
    public required string BlogDescription { get; init; }

    /// <summary>
    /// Base URL for the published site (e.g., https://example.com)
    /// Used for generating absolute URLs in sitemap and RSS feed
    /// </summary>
    public required string BaseUrl { get; init; } 
    
    /// <summary>
    ///     Specifies the output directory for generated static files.
    ///     Path is relative to the project root.
    /// </summary>
    public string OutputFolderPath { get; init; } = "output";

    /// <summary>
    ///     Defines the collection of pages that will be generated as static HTML files.
    /// </summary>
    public ImmutableList<PageToGenerate> PagesToGenerate { get; init; } = [];

    /// <summary>
    ///     When set to true, automatically includes non-parameterized Razor pages in the generation process.
    ///     
    ///     Examples:
    ///     - Non-parameterized: @page "/about"
    ///     - Parameterized: @page "/docs/{slug}"
    /// </summary>
    public bool AddPagesWithoutParameters { get; init; } = true;

    /// <summary>
    ///     Specifies the filename to use for index pages.
    ///     For example, a route "@page "/blog"" will generate "blog/index.html".
    /// </summary>
    public string IndexPageHtml { get; init; } = "index.html";

    /// <summary>
    ///     Specifies paths (files or directories) that should be excluded when copying content to the output folder.
    ///     
    ///     Paths are relative to the destination location in the output folder, not the source location.
    ///     
    ///     Example:
    ///     To ignore "wwwroot/app.css" when copying "wwwroot" to the root of the output folder,
    ///     add "app.css" to this list.
    /// </summary>
    public ImmutableList<string> IgnoredPathsOnContentCopy { get; init; } = [];

    /// <summary>
    ///     Customizes the YAML deserializer used for parsing front matter in markdown files.
    ///     
    ///     By default, uses camel case naming convention and ignores unmatched properties.
    /// </summary>
    public IDeserializer FrontMatterDeserializer { get; init; } = new DeserializerBuilder()
        .WithNamingConvention(CamelCaseNamingConvention.Instance)
        .IgnoreUnmatchedProperties()
        .Build();

    /// <summary>
    ///     Customizes the Markdown processing pipeline used for parsing markdown files.
    ///     
    ///     By default, includes advanced extensions and YAML front matter support.
    /// </summary>
    public MarkdownPipeline MarkdownPipeline { get; init; } = new MarkdownPipelineBuilder()
        .UseAdvancedExtensions()
        .UseYamlFrontMatter()
        .Build();

    /// <summary>
    ///     Controls whether file change detection and hot-reloading are enabled for the Blazor app.
    ///     When true, the app will automatically refresh when source files are modified.
    /// </summary>
    public bool HotReloadEnabled { get; init; } = true;

    /// <summary>
    ///     Registers an asynchronous action to be executed before file generation begins.
    ///     Multiple actions can be added and will be executed in the order they were registered.
    /// </summary>
    public ImmutableList<Func<Task>> BeforeFilesGenerationActions { get; init; } = [];
}