﻿using BlazorStatic.Models;
using BlazorStatic.Services.Content;
using BlazorStatic.Services.Content.MarkdigExtensions;
using BlazorStatic.Services.Content.Roslyn;
using BlazorStatic.Services.Generation;
using BlazorStatic.Services.Infrastructure;
using BlazorStatic.Services.Web;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;

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
    /// <typeparam name="TFrontMatter">The type used for Post metadata. Must implement IFrontMatter.</typeparam>
    /// <param name="services">The application's service collection.</param>
    /// <param name="configureOptions">Action to customize the content service options.</param>
    /// <returns>The updated service collection for method chaining.</returns>
    /// <remarks>
    /// <para>This method registers both concrete and interface implementations of the content service:</para>
    /// <list type="bullet">
    ///     <item><description>BlazorStaticContentService&lt;TFrontMatter&gt; as a concrete implementation</description></item>
    ///     <item><description>IContentPostService for general content Post access</description></item>
    ///     <item><description>IBlazorStaticContentOptions for configuration access</description></item>
    /// </list>
    /// <para>The service handles parsing, loading, and providing content with the specified front matter format.</para>
    /// </remarks>
    public static IServiceCollection AddBlazorStaticContentService<TFrontMatter>(this IServiceCollection services,
        Func<BlazorStaticContentOptions<TFrontMatter>>? configureOptions = null)
        where TFrontMatter : class, IFrontMatter, new()
    {
        var options = configureOptions?.Invoke() ?? new BlazorStaticContentOptions<TFrontMatter>();

        // Register options
        services.AddSingleton(options);

        // Register specialized services
        services.AddSingleton<TagService<TFrontMatter>>();
        services.AddSingleton<ContentFilesService<TFrontMatter>>();
        services.AddSingleton<MarkdownContentProcessor<TFrontMatter>>();

        // Register the primary service
        services.AddSingleton<BlazorStaticMarkdownContentService<TFrontMatter>>();
        services.AddSingleton<SitemapRssService>();

        // Register interface implementations
        services.AddSingleton<IBlazorStaticContentService>(provider =>
            provider.GetRequiredService<BlazorStaticMarkdownContentService<TFrontMatter>>());
        services.AddSingleton<IBlazorStaticContentOptions>(provider =>
            provider.GetRequiredService<BlazorStaticContentOptions<TFrontMatter>>());

        return services;
    }

    /// <summary>
    /// Registers the core BlazorStatic generation services for converting a Blazor application into static HTML, CSS, and JavaScript.
    /// </summary>
    /// <param name="services">The application's service collection.</param>
    /// <param name="configureOptions">Optional action to customize the static generation process.</param>
    /// <returns>The updated service collection for method chaining.</returns>
    /// <remarks>
    /// <para>This method registers several essential services for static site generation:</para>
    /// <list type="bullet">
    ///     <item><description>BlazorStaticHelpers for utility functions</description></item>
    ///     <item><description>BlazorStaticOptions for configuration</description></item>
    ///     <item><description>BlazorStaticService for the main generation process</description></item>
    ///     <item><description>BlazorStaticFileWatcher for monitoring file changes</description></item>
    /// </list>
    /// <para>Use this method in conjunction with UseBlazorStaticGenerator to complete the static site generation process.</para>
    /// </remarks>
    public static IServiceCollection AddBlazorStaticService(this IServiceCollection services,
        Func<BlazorStaticOptions> configureOptions)
    {
        var options = configureOptions.Invoke();

        services.AddSingleton(options);
        services.AddSingleton<BlazorStaticOutputGenerationService>();
        services.AddSingleton<BlazorFileWatcher>();
        services.AddSingleton<MarkdownParserService>();
        services.AddSingleton<RoutesHelperService>();
        services.AddSingleton<ConfigVerifier>();

        return services;
    }

    /// <summary>
    /// Adds a RoslynHighlighterService to the application's service collection with support for Roslyn highlighter configuration.
    /// </summary>
    /// <param name="services">The application's service collection.</param>
    /// <param name="configureOptions">Action to configure the Roslyn highlighter options.</param>
    /// <returns>The updated service collection for method chaining.</returns>
    /// <remarks>
    /// This method registers the RoslynHighlighterService and its associated options as singleton services.
    /// The RoslynHighlighterService facilitates syntax highlighting
    /// using Roslyn configuration provided in the specified options.
    /// </remarks>
    public static IServiceCollection AddRoslynService(this IServiceCollection services,
        Func<RoslynHighlighterOptions>? configureOptions = null)
    {
        var options = configureOptions?.Invoke() ?? new RoslynHighlighterOptions();

        services.AddSingleton(options);
        services.AddSingleton<RoslynHighlighterService>();

        if (options.ConnectedSolution != null)
        {
            services.AddSingleton<RoslynExampleCoordinator>();
            services.AddSingleton<CodeExecutionService>();
            services.AddSingleton<AssemblyLoaderService>();
        }

        return services;
    }

    private static void MapBlazorStaticAssets(this WebApplication app)
    {
        var optionList = app.Services.GetServices<IBlazorStaticContentOptions>().ToList();
        if (optionList.Count == 0)
        {
            throw new InvalidOperationException(
                "No BlazorStaticContentServices registered. Call AddBlazorStaticContentService<TFrontMatter> first.");
        }

        foreach (var option in optionList)
        {
            var combine = Path.Combine(Directory.GetCurrentDirectory(), option.ContentPath);
            app.UseStaticFiles(new StaticFileOptions
            {
                FileProvider = new PhysicalFileProvider(combine),
                RequestPath = "/" + option.PageUrl,
                ServeUnknownFileTypes = true,
            });
        }

        app.MapBlazorStaticSitemapRss();
    }

    /// <summary>
    /// Adds sitemap.xml and RSS feed endpoints to the application.
    /// </summary>
    /// <param name="app">The web application.</param>
    /// <returns>The web application for chaining.</returns>
    private static void MapBlazorStaticSitemapRss(this WebApplication app)
    {
        // Map the sitemap.xml endpoint
        app.MapGet("/sitemap.xml", async (SitemapRssService service) =>
        {
            var sitemap = await service.GenerateSitemap();
            // Set the content type and return the sitemap
            return Results.Content(sitemap, "application/xml");
        });

        // Map the rss.xml endpoint
        app.MapGet("/rss.xml", async (SitemapRssService service) =>
        {
            var rss = await service.GenerateRssFeed();
            return Results.Content(rss, "text/xml");
        });
    }

    /// <summary>
    /// Executes the static site generation process, converting the Blazor application to static files.
    /// </summary>
    /// <param name="app">The web application.</param>
    /// <returns>A Task representing the asynchronous generation operation.</returns>
    /// <remarks>
    /// <para>This method performs the complete static generation process:</para>
    /// <list type="number">
    ///     <item><description>Loads and parses all content from registered content services</description></item>
    ///     <item><description>Copies static web assets (from wwwroot and other static sources) to the output</description></item>
    ///     <item><description>Renders and saves all application routes as static HTML</description></item>
    /// </list>
    /// <para>Call this method after configuring all required BlazorStatic services and during application startup.
    /// The generation uses the first URL from the application's configured URLs list as the base address.</para>
    /// </remarks>
    private static async Task UseBlazorStaticGenerator(this WebApplication app)
    {
        var blazorStaticService = app.Services.GetRequiredService<BlazorStaticOutputGenerationService>();
        await blazorStaticService.GenerateStaticPages(app.Urls.First());
    }

    /// <summary>
    /// Conditionally runs the application or generates a static build based on command-line arguments.
    /// </summary>
    /// <param name="app">The web application.</param>
    /// <param name="args">Command-line arguments passed to the application.</param>
    /// <returns>A Task representing the asynchronous operation.</returns>
    /// <remarks>
    /// <para>This method provides a convenient way to toggle between development and build modes:</para>
    /// <list type="bullet">
    ///     <item><description>If the first argument is "build" (case-insensitive), the application starts, generates
    ///     static files using <see cref="UseBlazorStaticGenerator"/>, and then stops</description></item>
    ///     <item><description>Otherwise, the application runs normally with <see cref="WebApplication.RunAsync" /></description></item>
    /// </list>
    /// <para>In both scenarios, <see cref="MapBlazorStaticAssets"/> is called to configure static asset serving.</para>
    /// </remarks>
    public static async Task RunOrBuildBlazorStaticSite(this WebApplication app, string[] args)
    {
        // Verify configuration before proceeding
        var configVerifier = app.Services.GetRequiredService<ConfigVerifier>();
        var isConfigValid = configVerifier.VerifyConfiguration();
        if (!isConfigValid) return;

        app.MapBlazorStaticAssets();


        if (args.Length > 0 && args[0].Equals("build", StringComparison.OrdinalIgnoreCase))
        {
            await app.StartAsync();
            await app.UseBlazorStaticGenerator();
            await app.StopAsync();
        }
        else
        {
            await app.RunAsync();
        }
    }
}