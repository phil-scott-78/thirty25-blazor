using BlazorStatic;
using Markdig;
using Markdig.Extensions.AutoIdentifiers;
using Thirty25.Web;
using Thirty25.Web.BlogServices;
using Thirty25.Web.BlogServices.Markdown;
using Thirty25.Web.Components;


var builder = WebApplication.CreateBuilder(args);

builder.WebHost.UseStaticWebAssets();
builder.Services.AddRazorComponents();

// configures site wide settings
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
            .UseSyntaxHighlighting(roslynHighlighter)
            .UseYamlFrontMatter()
            .Build();
    }
});

// configures individual sections of the blog. PageUrl should match the configured razor pages route
// and contentPath should match the location on disk.
// you can have multiple of these per site.
builder.Services.AddBlazorStaticContentService(() => new BlazorStaticContentOptions<FrontMatter>
{
    PageUrl = "blog",
    ContentPath = "Content/Blog",
});

// custom service for doing css work
builder.Services.AddSingleton<MonorailCssService>();
// custom service for highlighting C# code blocks at generation time
builder.Services.AddSingleton<RoslynHighlighterService>(sp => new RoslynHighlighterService("../../thirty25-blazor.sln", "../../blog-projects/", sp.GetRequiredService<ILogger<RoslynHighlighterService>>()));

var app = builder.Build();
app.UseHttpsRedirection();
app.MapStaticAssets();
app.UseAntiforgery();
app.MapRazorComponents<App>();

// custom css. the blazor static service will discover the mapped url automatically
// and include it with the static generation.
app.MapGet("/styles.css", async (MonorailCssService cssService) => Results.Content(await cssService.GetStyleSheet(), "text/css"));
await app.RunOrBuildBlazorStaticSite(args);