using BlazorStatic;
using BlazorStatic.Services.Content.MarkdigExtensions;
using BlazorStatic.Services.Content.MarkdigExtensions.CodeHighlighting;
using BlazorStatic.Services.Content.MarkdigExtensions.Tabs;
using BlazorStatic.Services.Content.Roslyn;
using Markdig;
using Markdig.Extensions.AutoIdentifiers;
using Thirty25.Web;
using Thirty25.Web.BlogServices.Styling;
using Thirty25.Web.Components;
using MonorailCssService = Thirty25.Web.BlogServices.Styling.MonorailCssService;

var builder = WebApplication.CreateBuilder(args);

builder.WebHost.UseStaticWebAssets();
builder.Services.AddRazorComponents();
Console.WriteLine("Program.cs");

// configures site wide settings
// hot reload note - these will not be reflected until the application restarts
builder.Services.AddBlazorStaticService(() => new BlazorStaticOptions
{
    BlogTitle = "Thirty25",
    BlogDescription = "Quite exciting this computer magic",
    BaseUrl = "https://thirty25.blog",
    MarkdownPipelineBuilder = serviceProvider =>
    {
        var roslynHighlighter = serviceProvider.GetRequiredService<RoslynHighlighterService>();
        return new MarkdownPipelineBuilder()
            .UseAutoIdentifiers(AutoIdentifierOptions.GitHub) // This sets up GitHub-style header IDs
            .UseAdvancedExtensions()
            .UseSyntaxHighlighting(roslynHighlighter, new CodeHighlightRenderOptions
            {
                OuterWrapperCss = "not-prose",
                StandaloneContainerCss =
                    "bg-base-800 dark:bg-primary-950/90 border border-primary-700/50 shadow rounded rounded-xl overflow-x-auto",
                PreBaseCss =
                    "p-1 overflow-x-auto scheme-dark font-mono text-xs md:text-sm font-light leading-relaxed w-full",
                PreStandaloneCss = "text-base-100/90 py-2 px-2 md:px-4"
            })
            .UseTabbedCodeBlocks(new TabbedCodeBlockRenderOptions()
            {
                OuterWrapperCss = "not-prose",
                ContainerCss =
                    "flex flex-col bg-base-800 dark:bg-primary-950/25 border border-primary-700/50 shadow rounded rounded-xl overflow-x-auto",
                TabListCss = "flex flex-row flex-wrap px-4 pt-1 bg-base-900/95 dark:bg-primary-900/25 space-x-4",
                TabButtonCss =
                    "whitespace-nowrap border-b border-transparent py-2 text-xs text-base-100/90 font-medium transition-colors hover:text-accent-300 disabled:pointer-events-none disabled:opacity-50 aria-selected:text-accent-400 aria-selected:border-accent-400",
                TabPanelCss = "tab-panel hidden aria-selected:block py-3 px-2 md:px-4"
            })
            .UseYamlFrontMatter()
            .Build();
    }
});

// configures individual sections of the blog. PageUrl should match the configured razor pages route,
// and contentPath should match the location on disk.
// you can have multiple of these per site.
builder.Services.AddBlazorStaticContentService(() => new BlazorStaticContentOptions<BlogFrontMatter>
{
    PageUrl = "blog",
    ContentPath = "Content/Blog",
});

builder.Services.AddRoslynService(() => new RoslynHighlighterOptions()
{
    ConnectedSolution = new ConnectedDotNetSolution
     {
         SolutionPath = "../../thirty25-blazor.sln",
         ProjectsPath = "../../blog-projects/"
     }
});

// custom service for doing CSS work
builder.Services.AddSingleton<MonorailCssService>();
builder.Services.AddSingleton<CssClassCollector>();

var app = builder.Build();
app.UseHttpsRedirection();
app.MapStaticAssets();
app.UseAntiforgery();
app.MapRazorComponents<App>();

// custom css. the blazor static service will discover the mapped url automatically
// and include it with the static generation.
app.UseMiddleware<CssClassCollectorMiddleware>();
app.MapGet("/styles.css", (MonorailCssService cssService) => Results.Content(cssService.GetStyleSheet(), "text/css"));

await app.RunOrBuildBlazorStaticSite(args);