using BlazorStatic;
using BlazorStatic.Services.Content.Roslyn;
using Thirty25.Web;
using Thirty25.Web.BlogServices.Styling;
using Thirty25.Web.Components;

var builder = WebApplication.CreateBuilder(args);

builder.WebHost.UseStaticWebAssets();
builder.Services.AddRazorComponents();

// configures site wide settings
// hot reload note - these will not be reflected until the application restarts
builder.Services.AddBlazorStaticService(() => new BlazorStaticOptions
{
    BlogTitle = "Thirty25",
    BlogDescription = "Quite exciting this computer magic",
    BaseUrl = "https://thirty25.blog",
});

// configures individual sections of the blog. PageUrl should match the configured razor pages route,
// and contentPath should match the location on disk.
// you can have multiple of these per site.
builder.Services.AddBlazorStaticContentService<BlogFrontMatter>();

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

// custom CSS. the blazor static service will discover the mapped url automatically
// and include it with the static generation.
app.UseMiddleware<CssClassCollectorMiddleware>();
app.MapGet("/styles.css", (MonorailCssService cssService) => Results.Content(cssService.GetStyleSheet(), "text/css"));

await app.RunOrBuildBlazorStaticSite(args);