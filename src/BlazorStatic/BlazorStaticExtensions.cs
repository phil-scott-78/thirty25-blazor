using BlazorStatic.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging;

namespace BlazorStatic;

/// <summary>
/// Provides extension methods for configuring and using BlazorStatic services within an ASP.NET Core application.
/// These methods facilitate static site generation with Blazor, including content management and file processing.
/// </summary>
public static class BlazorStaticExtensions
{
    /// <summary>
    /// Adds a BlazorStaticContentService to the application's service collection with custom front matter support.
    /// </summary>
    /// <typeparam name="TFrontMatter">The type used for post metadata. Must implement IFrontMatter.</typeparam>
    /// <param name="services">The application's service collection.</param>
    /// <param name="configureOptions">Optional action to customize the content service options. If not provided, default blog settings are used.</param>
    /// <returns>The updated service collection for method chaining.</returns>
    /// <remarks>
    /// This method registers both concrete and interface implementations of the content service:
    /// - BlazorStaticContentService&lt;TFrontMatter&gt; as a concrete implementation
    /// - IContentPostService for general content post access
    /// - IBlazorStaticContentOptions for configuration access
    /// 
    /// The service handles parsing, loading, and providing content with the specified front matter format.
    /// </remarks>
    public static IServiceCollection AddBlazorStaticContentService<TFrontMatter>(this IServiceCollection services,
        Action<BlazorStaticContentOptions<TFrontMatter>>? configureOptions = null)
        where TFrontMatter : class, IFrontMatter, new()
    {
        BlazorStaticContentOptions<TFrontMatter> options = new();
        configureOptions?.Invoke(options);
        options.CheckOptions();

        services.AddSingleton(options);
        services.AddSingleton<BlazorStaticContentService<TFrontMatter>>();
        
        // also include their interface, we'll need these for loading all at once
        services.AddSingleton<IContentPostService>(provider => provider.GetRequiredService<BlazorStaticContentService<TFrontMatter>>());
        services.AddSingleton<IBlazorStaticContentOptions>(provider => provider.GetRequiredService<BlazorStaticContentOptions<TFrontMatter>>());
        return services;
    }

    /// <summary>
    /// Registers the core BlazorStatic generation services for converting a Blazor application into static HTML, CSS, and JavaScript.
    /// </summary>
    /// <param name="services">The application's service collection.</param>
    /// <param name="configureOptions">Optional action to customize the static generation process.</param>
    /// <returns>The updated service collection for method chaining.</returns>
    /// <remarks>
    /// This method registers several essential services for static site generation:
    /// - BlazorStaticHelpers for utility functions
    /// - BlazorStaticOptions for configuration
    /// - BlazorStaticService for the main generation process
    /// - BlazorStaticFileWatcher for monitoring file changes
    /// 
    /// Use this method in conjunction with UseBlazorStaticGenerator to complete the static site generation process.
    /// </remarks>
    public static IServiceCollection AddBlazorStaticService(this IServiceCollection services,
        Action<BlazorStaticOptions>? configureOptions = null)
    {
        services.AddSingleton<BlazorStaticHelpers>();
        var options = new BlazorStaticOptions();
        configureOptions?.Invoke(options);

        services.AddSingleton(options);
        services.AddSingleton<BlazorStaticService>();
        services.AddSingleton<BlazorStaticFileWatcher>();

        return services;
    }

    /// <summary>
    /// Configures the application to serve media files defined in BlazorStatic content options.
    /// </summary>
    /// <param name="app">The web application.</param>
    /// <remarks>
    /// This method scans all registered IBlazorStaticContentOptions instances and sets up static file serving
    /// for any configured media paths. For each valid configuration:
    /// 
    /// 1. Constructs a web-accessible request path from the MediaRequestPath
    /// 2. Maps the physical media folder (ContentPath + MediaFolderRelativeToContentPath) to this request path
    /// 3. Logs a warning if the configured media folder doesn't exist
    /// 
    /// This enables serving media files (images, documents, etc.) associated with your static content.
    /// </remarks>
    public static void MapBlazorStaticAssets(this WebApplication app)
    {
        var optionList = app.Services.GetServices<IBlazorStaticContentOptions>().ToList();
        if (optionList.Count == 0)
        {
            throw new InvalidOperationException("No BlazorStaticContentServices registered. Call AddBlazorStaticContentService<TFrontMatter> first.");
        }
        
        foreach (var option in optionList)
        {
            if (option.MediaRequestPath is null || option.MediaFolderRelativeToContentPath is null)
            {
                continue;
            }
            
            var requestPath = "/" + Path.GetFullPath(option.MediaRequestPath)[Directory.GetCurrentDirectory().Length..]
                .TrimStart(Path.DirectorySeparatorChar)
                .Replace("\\", "/");


            var realPath = Path.Combine(Directory.GetCurrentDirectory(), option.ContentPath, option.MediaFolderRelativeToContentPath);
            if(!Directory.Exists(realPath))
            {
                app.Logger.LogWarning("folder for media path ({Folder}) doesn't exist", realPath);
            }
            else
            {
                app.UseStaticFiles(new StaticFileOptions
                {
                    FileProvider = new PhysicalFileProvider(realPath),
                    RequestPath = requestPath
                });
            }

        }
        
    }

    /// <summary>
    /// Executes the static site generation process, converting the Blazor application to static files.
    /// </summary>
    /// <param name="app">The web application.</param>
    /// <returns>A Task representing the asynchronous generation operation.</returns>
    /// <remarks>
    /// This method performs the complete static generation process:
    /// 
    /// 1. Loads and parses all content from registered content services
    /// 2. Copies static web assets (from wwwroot and other static sources) to the output
    /// 3. Renders and saves all application routes as static HTML
    /// 
    /// Call this method after configuring all required BlazorStatic services and during application startup.
    /// The generation uses the first URL from the application's configured URLs list as the base address.
    /// </remarks>
    public static async Task UseBlazorStaticGenerator(this WebApplication app)
    {
        var blazorStaticService = app.Services.GetRequiredService<BlazorStaticService>();
        var blazorStaticContentService = app.Services.GetServices<IContentPostService>();
        foreach (var contentService in blazorStaticContentService)
        {
            contentService.ParseAndAddPosts();
        }

        //adds wwwroot files (or any other files that has been added as static content) to the output
        AddStaticWebAssetsToOutput(app.Environment.WebRootFileProvider, string.Empty, blazorStaticService);

        await blazorStaticService.GenerateStaticPages(app.Urls.First());
    }

    /// <summary>
    ///     Takes the provider, search it recursively and add all the files found.
    /// </summary>
    /// <param name="fileProvider"></param>
    /// <param name="subPath"></param>
    /// <param name="blazorStaticService"></param>
    private static void AddStaticWebAssetsToOutput(IFileProvider fileProvider, string subPath, BlazorStaticService blazorStaticService)
    {
        var contents = fileProvider.GetDirectoryContents(subPath);

        foreach(var item in contents)
        {
            var fullPath = $"{subPath}{item.Name}";
            if(item.IsDirectory)
            {
                AddStaticWebAssetsToOutput(fileProvider, $"{fullPath}/", blazorStaticService);
            }
            else
            {
                if(item.PhysicalPath is not null)
                {
                    blazorStaticService.AddContentToCopyToOutput(new ContentToCopy(item.PhysicalPath, fullPath));
                }
            }
        }
    }
}
