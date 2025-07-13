using MonorailCss;
using MyLittleContentEngine.BlogSite;
using MyLittleContentEngine.BlogSite.Components;
using MyLittleContentEngine.Services.Content;
using Thirty25.Web;

var builder = WebApplication.CreateBuilder(args);


builder.Services.AddBlogSite(_ => new BlogSiteOptions
{
    SiteTitle = "Thirty25",
    AuthorName = "Phil Scott",
    Description = "Quite exciting this computer magic",
    BaseUrl = Environment.GetEnvironmentVariable("BaseHref") ?? "/",
    CanonicalBaseUrl = Environment.GetEnvironmentVariable("CanonicalBaseHref") ?? "https://thirty25.blog",
    PrimaryHue = 240,
    BaseColorName = ColorNames.Zinc,
    AdditionalRoutingAssemblies = [typeof(Program).Assembly],
    SolutionPath = "../../thirty25-blazor.sln",
    ContentRootPath = "Content",
    BlogContentPath = "Blog",
    BlogBaseUrl = "/Blog",
    TagsPageUrl = "/tags",
    DisplayFontFamily = "\"Inter\", sans-serif",
    BodyFontFamily = "\"Inter\", sans-serif",
    EnableRss = true,
    EnableSitemap = true,
    AdditionalHtmlHeadContent = """
                                <link rel="preconnect" href="https://fonts.googleapis.com">
                                <link rel="preconnect" href="https://fonts.gstatic.com" crossorigin>
                                <link href="https://fonts.googleapis.com/css2?family=Inter:ital,opsz,wght@0,14..32,100..900;1,14..32,100..900&display=swap" rel="stylesheet">
                                """,
    // Custom hero content
    HeroContent = new HeroContent("Software dev, tinkerer, and stay-at-home dad.", "I'm <strong>Phil Scott</strong>, a software designer and entrepreneur based in Ohio. I was once a .NET developer and a current stay-at-home Dad. I clean up their messes then create my own here. Honestly? I'm just trying to learn to develop video games to impress my toddlers."),
    MyWork = [
        new Project("MyLittleContentEngine", "An inflexible and opinionated static content generator written in .NET. It does dotnet watch pretty well though", "https://github.com/phil-scott-78/MyLittleContentEngine"),
        new Project("MonorailCSS", "MonorailCSS is a utility-first CSS library inspired heavily by Tailwind for .NET", "https://github.com/monorailcss/MonorailCss.Framework"),
        new Project("Mdazor", "A Markdig extension that lets you embed Blazor components directly in Markdown", "https://github.com/phil-scott-78/Mdazor"),
        new Project("RedPajama", "Gbnf Generator from C# types.", "https://github.com/phil-scott-78/RedPajama"),
    ],
    Socials = new[]
    {
        new SocialLink(SocialIcons.GithubIcon, "https://github.com/phil-scott-78"),
        new SocialLink(SocialIcons.BlueskyIcon, "https://bsky.app/profile/philco.bsky.social")
    },
    MainSiteLinks = [],
    SocialMediaImageUrlFactory = page => $"social-images/{SocialImageService.GenerateSocialFilename(page.Url)}"
});

builder.Services.AddSingleton<SocialImageService>();
builder.Services.AddSingleton<IContentService>(provider => provider.GetRequiredService<SocialImageService>());

var app = builder.Build();
app.MapStaticAssets();

app.UseBlogSite();

await app.RunBlogSiteAsync(args);

