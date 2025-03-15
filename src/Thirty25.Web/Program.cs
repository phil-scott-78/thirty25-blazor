using BlazorStatic;
using MonorailCss;
using Thirty25.Web;
using Thirty25.Web.Components;

var builder = WebApplication.CreateBuilder(args);

builder.WebHost.UseStaticWebAssets();
builder.Services.AddBlazorStaticService(options =>
{
    options.PagesToGenerate.Add(new PageToGenerate("/styles.css", "styles.css"));
    options.HotReloadEnabled = true;
});
builder.Services.AddBlazorStaticContentService<FrontMatter>();
builder.Services.AddRazorComponents();
builder.Services.AddSingleton<MonorailCssService>();

var app = builder.Build();
app.UseHttpsRedirection();
app.MapGet("/styles.css", async (MonorailCssService cssService) => Results.Content(await cssService.GetStyleSheet(), "text/css") );
app.MapStaticAssets();
app.UseAntiforgery();
app.MapRazorComponents<App>();
app.MapBlazorStaticAssets();

if (args.Length > 0 && args[0].Equals("build", StringComparison.OrdinalIgnoreCase))
{
    await app.StartAsync();
    await app.UseBlazorStaticGenerator();
    await app.StopAsync();
}
else
{
    app.Run();    
}


public static class WebsiteKeys
{
    public const string GitHubRepo = "https://github.com/phil-scott-78/thirty25-blazor";
    public const string Title = "Thirty25";
    public const string BlogPostStorageAddress = $"{GitHubRepo}/tree/main/src/Thirty25.Web/Content/Blog";
    public const string BlogLead = "Quite exciting this computer magic";
}