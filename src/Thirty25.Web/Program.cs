using Microsoft.Extensions.DependencyInjection.Extensions;
using MonorailCss;
using MyLittleContentEngine;
using MyLittleContentEngine.MonorailCss;
using MyLittleContentEngine.Services.Content;
using MyLittleContentEngine.Services.Content.Roslyn;
using Thirty25.Web;
using Thirty25.Web.Components;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents();

// configures site wide settings
// hot reload note - these will not be reflected until the application restarts
builder.Services.AddContentEngineService(_ => new ContentEngineOptions
{
    SiteTitle = "Thirty25",
    SiteDescription = "Quite exciting this computer magic",
    BaseUrl =  Environment.GetEnvironmentVariable("BaseHref") ?? "/",
    CanonicalBaseUrl = Environment.GetEnvironmentVariable("CanonicalBaseHref") ?? "https://thirty25.blog",
});
builder.Services.AddSingleton<SocialImageService>();
builder.Services.AddSingleton<IContentService>(provider => provider.GetRequiredService<SocialImageService>());
// configures individual sections of the blog. PageUrl should match the configured razor pages route,
// and contentPath should match the location on disk.
// you can have multiple of these per site.
builder.Services.AddContentEngineStaticContentService(_ => new ContentEngineContentOptions<BlogFrontMatter>()
{
    ContentPath = "Content/Blog",
    BasePageUrl = "/Blog",
    Tags = new TagsOptions()
    {
        TagsPageUrl = "/tags"
    }
});
builder.Services.AddMonorailCss(_ =>
{
    return new MonorailCssOptions
    {
        PrimaryHue = () => 250 ,
        CustomCssFrameworkSettings = settings => settings with
        {
            DesignSystem = settings.DesignSystem with
            {
                FontFamilies = settings.DesignSystem.FontFamilies
                    .Add("display", new FontFamilyDefinition("\"Lexend Deca\", sans-serif"))
                    .SetItem("sans", new FontFamilyDefinition("\"Lexend Deca\", sans-serif"))
            }
        }
    };
});

builder.Services.AddRoslynService(_ => new RoslynHighlighterOptions()
{
    ConnectedSolution = new ConnectedDotNetSolution
    {
        SolutionPath = "../../thirty25-blazor.sln",
    }
});

// custom service for doing CSS work


var app = builder.Build();
app.UseAntiforgery();
app.MapRazorComponents<App>();
app.UseMonorailCss();
app.MapStaticAssets();
await app.RunOrBuildContent(args);
