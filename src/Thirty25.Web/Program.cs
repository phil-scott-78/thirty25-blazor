using BlazorStatic;
using BlazorStatic.Models;
using Thirty25.Web;
using Thirty25.Web.Components;

var builder = WebApplication.CreateBuilder(args);

builder.WebHost.UseStaticWebAssets();
builder.Services.AddSingleton<RoslynHighlighterService>();
builder.Services.AddBlazorStaticService(() => new BlazorStaticOptions()
{
    BlogTitle = "Thirty25",
    BlogDescription = "Quite exciting this computer magic",
    BaseUrl = "https://thirty25.com",
    HotReloadEnabled = true,
    PagesToGenerate = [new PageToGenerate("/styles.css", "styles.css")]
});
builder.Services.AddBlazorStaticContentService(() => new BlazorStaticContentOptions<FrontMatter>()
{
    PageUrl = "blog",
    PostProcessMarkdown = (serviceProvider, f, s) =>
    {
        var roslyn = serviceProvider.GetRequiredService<RoslynHighlighterService>();
        return (f, roslyn.Highlight(s));
    } 
});
builder.Services.AddRazorComponents();
builder.Services.AddSingleton<MonorailCssService>();

var app = builder.Build();
app.UseHttpsRedirection();
app.MapGet("/styles.css", async (MonorailCssService cssService) => Results.Content(await cssService.GetStyleSheet(), "text/css"));
app.MapStaticAssets();
app.UseAntiforgery();
app.MapRazorComponents<App>();

await app.RunOrBuildBlazorStaticSite(args);