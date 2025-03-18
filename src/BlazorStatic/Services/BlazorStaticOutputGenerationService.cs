using System.Collections.Immutable;
using Microsoft.Extensions.Logging;
using System.Reflection;
using BlazorStatic.Models;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.FileProviders;

namespace BlazorStatic.Services;

/// <summary>
///     Service responsible for generating static HTML pages from a Blazor application.
///     This enables server-side rendered Blazor applications to be deployed as static websites.
/// </summary>
/// <param name="environment">The web hosting environment providing access to web root files</param>
/// <param name="contentPostServiceCollection">Collection of content services providing pages to generate and content to copy</param>
/// <param name="routeHelper">Service for discovering configured ASP.NET routes.</param>
/// <param name="options">Configuration options for the static generation process</param>
/// <param name="logger">Logger for diagnostic output</param>
internal class BlazorStaticOutputGenerationService(
    IWebHostEnvironment environment,
    IEnumerable<IBlazorStaticContentService> contentPostServiceCollection,
    RoutesHelperService routeHelper,
    BlazorStaticOptions options,
    ILogger<BlazorStaticOutputGenerationService> logger)
{
    /// <summary>
    /// Generates static HTML pages for the Blazor application.
    /// </summary>
    /// <param name="appUrl">The base URL of the running Blazor application, used for making HTTP requests to fetch page content</param>
    /// <returns>A task representing the asynchronous operation</returns>
    /// <exception cref="InvalidOperationException">Thrown when no pages are available to generate</exception>
    /// <remarks>
    /// <para>
    /// This method performs several key operations to generate static HTML files from a running Blazor application.
    /// </para>
    /// <list type="number">
    ///     <item>
    ///         <description>Collects pages to generate from all registered content services</description>
    ///     </item>
    ///     <item>
    ///         <description>Optionally adds routes registered via MapGet based on configuration</description>
    ///     </item>
    ///     <item>
    ///         <description>Optionally adds non-parametrized Razor pages based on configuration</description>
    ///     </item>
    ///     <item>
    ///         <description>Clears and recreates the output directory</description>
    ///     </item>
    ///     <item>
    ///         <description>Generates a sitemap.xml file if configured</description>
    ///     </item>
    ///     <item>
    ///         <description>Copies static content (wwwroot files, etc.) to the output directory</description>
    ///     </item>
    ///     <item>
    ///         <description>Renders each page by making HTTP requests to the running application</description>
    ///     </item>
    ///     <item>
    ///         <description>Saves each page as a static HTML file in the output directory</description>
    ///     </item>
    /// </list>
    /// </remarks>
    internal async Task GenerateStaticPages(string appUrl)
    {
        // Collect pages to generate from content services and options
        var pagesToGenerate = contentPostServiceCollection
            .Aggregate(ImmutableList<PageToGenerate>.Empty,
                (current, item) => current.AddRange(item.GetPagesToGenerate()));

        pagesToGenerate = pagesToGenerate.AddRange(options.PagesToGenerate);
        pagesToGenerate = pagesToGenerate.AddRange(routeHelper.GetMapGetRoutes());

        // Optionally discover and add non-parametrized Razor pages
        if (options.AddPagesWithoutParameters)
        {
            pagesToGenerate = pagesToGenerate.AddRange(GetPagesWithoutParameters());
        }

        // Clear and recreate output directory
        if (Directory.Exists(options.OutputFolderPath))
        {
            Directory.Delete(options.OutputFolderPath, true);
        }

        Directory.CreateDirectory(options.OutputFolderPath);

        // Prepare paths to ignore during content copy
        var ignoredPathsWithOutputFolder = options
            .IgnoredPathsOnContentCopy
            .Select(x => Path.Combine(options.OutputFolderPath, x))
            .ToList();

        var contentToCopy = contentPostServiceCollection
            .SelectMany(service => service.GetContentToCopy())
            .Concat(GetStaticWebAssetsToOutput(environment.WebRootFileProvider, string.Empty))
            .ToImmutableList();

        // Copy all content to output directory
        foreach (var pathToCopy in contentToCopy)
        {
            var targetPath = Path.Combine(options.OutputFolderPath, pathToCopy.TargetPath);

            logger.LogInformation("Copying {sourcePath} to {targetPath}", pathToCopy.SourcePath, targetPath);
            CopyContent(pathToCopy.SourcePath, targetPath, ignoredPathsWithOutputFolder);
        }

        // Create HTTP client for fetching rendered pages
        using HttpClient client = new();
        client.BaseAddress = new Uri(appUrl);

        // Generate each page by making HTTP requests and saving the response
        foreach (var page in pagesToGenerate)
        {
            logger.LogInformation("Generating {pageUrl} into {pageOutputFile}", page.Url, page.OutputFile);
            string content;
            try
            {
                content = await client.GetStringAsync(page.Url);
            }
            catch (HttpRequestException ex)
            {
                logger.LogWarning(
                    "Failed to retrieve page at {pageUrl}. StatusCode:{statusCode}. Error: {exceptionMessage}",
                    page.Url,
                    ex.StatusCode, ex.Message);

                continue;
            }

            var outFilePath = Path.Combine(options.OutputFolderPath, page.OutputFile.TrimStart('/'));

            var directoryPath = Path.GetDirectoryName(outFilePath);
            if (!string.IsNullOrEmpty(directoryPath))
            {
                Directory.CreateDirectory(directoryPath);
            }

            await File.WriteAllTextAsync(outFilePath, content);
        }
    }

    /// <summary>
    ///     Recursively collects all static web assets (files in wwwroot) that should be copied to the output directory.
    /// </summary>
    /// <param name="fileProvider">File provider for accessing static web assets</param>
    /// <param name="subPath">Current sub-path being processed</param>
    /// <returns>Collection of content items to copy</returns>
    private static IEnumerable<ContentToCopy> GetStaticWebAssetsToOutput(IFileProvider fileProvider, string subPath)
    {
        var contents = fileProvider.GetDirectoryContents(subPath);

        foreach (var item in contents)
        {
            var fullPath = $"{subPath}{item.Name}";
            if (item.IsDirectory)
            {
                foreach (var file in GetStaticWebAssetsToOutput(fileProvider, $"{fullPath}/"))
                {
                    yield return file;
                }
            }
            else
            {
                if (item.PhysicalPath is not null)
                {
                    yield return new ContentToCopy(item.PhysicalPath, fullPath);
                }
            }
        }
    }

    /// <summary>
    ///     Discovers and returns all non-parametrized Razor pages that should be generated.
    ///     Uses reflection to find routes defined in the entry assembly.
    /// </summary>
    /// <returns>Collection of pages to generate</returns>
    private IEnumerable<PageToGenerate> GetPagesWithoutParameters()
    {
        var entryAssembly = Assembly.GetEntryAssembly()!;
        var routesToGenerate = routeHelper.GetRoutesToRender(entryAssembly);

        foreach (var route in routesToGenerate)
        {
            yield return new PageToGenerate(route, Path.Combine(route, options.IndexPageHtml));
        }
    }

    /// <summary>
    ///     Copies content from a source path to a target path, respecting the ignored paths list.
    ///     Handles both files and directories recursively.
    /// </summary>
    /// <param name="sourcePath">Source path of file or directory to copy</param>
    /// <param name="targetPath">Target path where content should be copied</param>
    /// <param name="ignoredPaths">List of paths that should be excluded from copying</param>
    private void CopyContent(string sourcePath, string targetPath, List<string> ignoredPaths)
    {
        if (ignoredPaths.Contains(targetPath))
        {
            return;
        }

        // Handle single file copy
        if (File.Exists(sourcePath))
        {
            var dir = Path.GetDirectoryName(targetPath);
            if (dir == null)
            {
                return;
            }

            Directory.CreateDirectory(dir);
            File.Copy(sourcePath, targetPath, true);
            return;
        }

        // Validate source directory exists
        if (!Directory.Exists(sourcePath))
        {
            logger.LogError("Source path ({sourcePath}) does not exist", sourcePath);
            return;
        }

        // Create target directory if needed
        if (!Directory.Exists(targetPath))
        {
            Directory.CreateDirectory(targetPath);
        }

        var ignoredPathsWithTarget = ignoredPaths.Select(x => Path.Combine(targetPath, x)).ToList();

        // Create all subdirectories first (except ignored ones)
        foreach (var dirPath in Directory.GetDirectories(sourcePath, "*", SearchOption.AllDirectories))
        {
            var newDirPath = ChangeRootFolder(dirPath);
            if (ignoredPathsWithTarget.Contains(newDirPath))
            {
                continue;
            }

            Directory.CreateDirectory(newDirPath);
        }

        // Copy all files (except those in ignored paths)
        foreach (var newPath in Directory.GetFiles(sourcePath, "*.*", SearchOption.AllDirectories))
        {
            var newPathWithNewDir = ChangeRootFolder(newPath);
            if (ignoredPathsWithTarget.Contains(newPathWithNewDir)
                || !Directory.Exists(Path.GetDirectoryName(newPathWithNewDir)))
            {
                continue;
            }

            File.Copy(newPath, newPathWithNewDir, true);
        }

        return;

        // Helper function to transform path from source root to target root
        string ChangeRootFolder(string dirPath)
        {
            var relativePath = dirPath[sourcePath.Length..].TrimStart(Path.DirectorySeparatorChar);
            return Path.Combine(targetPath, relativePath);
        }
    }
}