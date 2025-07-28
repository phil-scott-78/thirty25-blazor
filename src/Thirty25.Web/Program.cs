using MonorailCss;
using MyLittleContentEngine.BlogSite;
using MyLittleContentEngine.BlogSite.Components;
using MyLittleContentEngine.Services.Content;
using Thirty25.Web;

var builder = WebApplication.CreateBuilder(args);


builder.Services.AddBlogSite(_ => new BlogSiteOptions()
{
    ApplicationArgs = args,
    SiteTitle = "Thirty25",
    AuthorName = "Phil Scott",
    Description = "Quite exciting this computer magic",
    CanonicalBaseUrl = Environment.GetEnvironmentVariable("CanonicalBaseHref") ?? "https://thirty25.blog",
    PrimaryHue = 240,
    BaseColorName = ColorNames.Zinc,
    AdditionalRoutingAssemblies = [typeof(Program).Assembly],
    SolutionPath = "../../thirty25-blazor.sln",
    BlogContentPath = "blog",
    BlogBaseUrl = "/blog",
    TagsPageUrl = "/tags",
    DisplayFontFamily = "\"Inter\", sans-serif",
    BodyFontFamily = "\"Inter\", sans-serif",
    ExtraStyles = """
                  @font-face {
                    font-family: 'Inter';
                    font-style: normal;
                    font-weight: 100 900;
                    font-display: swap;
                    src: url(fonts/inter.woff2) format('woff2');
                    unicode-range: U+0000-00FF, U+0131, U+0152-0153, U+02BB-02BC, U+02C6, U+02DA, U+02DC, U+0304, U+0308, U+0329, U+2000-206F, U+20AC, U+2122, U+2191, U+2193, U+2212, U+2215, U+FEFF, U+FFFD;
                  }
                  """,
    EnableRss = true,
    EnableSitemap = true,
    // Custom hero content
    HeroContent = new HeroContent("Software dev, tinkerer, and stay-at-home dad.", "I'm <strong>Phil Scott</strong>, a software designer and entrepreneur based in Ohio. I was once a .NET developer and a current stay-at-home Dad. I clean up their messes then create my own here. Honestly? I'm just trying to learn to develop video games to impress my toddlers."),
    MyWork = [
        new Project("MyLittleContentEngine", "An inflexible and opinionated static content generator written in .NET. It does dotnet watch pretty well though", "https://github.com/phil-scott-78/MyLittleContentEngine"),
        new Project("MonorailCSS", "MonorailCSS is a utility-first CSS library inspired heavily by Tailwind for .NET", "https://github.com/monorailcss/MonorailCss.Framework"),
        new Project("Mdazor", "A Markdig extension that lets you embed Blazor components directly in Markdown", "https://github.com/phil-scott-78/Mdazor"),
        new Project("RedPajama", "Gbnf Generator from C# types.", "https://github.com/phil-scott-78/RedPajama"),
        new Project("CooklangSharp", "A .NET parser for the Cooklang recipe markup language.", "https://github.com/phil-scott-78/CooklangSharp"),
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

