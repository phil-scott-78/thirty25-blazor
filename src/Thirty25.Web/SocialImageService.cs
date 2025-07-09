using System.Collections.Immutable;
using Microsoft.Playwright;
using MyLittleContentEngine.Models;
using MyLittleContentEngine.Services.Content;
using MyLittleContentEngine.Services.Content.TableOfContents;

namespace Thirty25.Web;

public class SocialImageService(IMarkdownContentService<BlogFrontMatter> content) : IContentService
{
    public Task<ImmutableList<PageToGenerate>> GetPagesToGenerateAsync() =>
        Task.FromResult(ImmutableList<PageToGenerate>.Empty);

    public Task<ImmutableList<ContentTocItem>> GetContentTocEntriesAsync() =>
        Task.FromResult(ImmutableList<ContentTocItem>.Empty);

    public Task<ImmutableList<ContentToCopy>> GetContentToCopyAsync() =>
        Task.FromResult(ImmutableList<ContentToCopy>.Empty);

    public Task<ImmutableList<CrossReference>> GetCrossReferencesAsync() =>
        Task.FromResult(ImmutableList<CrossReference>.Empty);

    public async Task<ImmutableList<ContentToCreate>> GetContentToCreateAsync()
    {
        var png = ConvertPngToBase64ImgTag("social-bg.png");
        
        var contentToCreate = new List<ContentToCreate>();

        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync();
        var browserContext = await browser.NewContextAsync(new BrowserNewContextOptions
            { ViewportSize = new ViewportSize { Width = 1200, Height = 630 } });
        var page = await browserContext.NewPageAsync();

        var pages = await content.GetAllContentPagesAsync();
        foreach (var contentPage in pages)
        {
            var filename = GenerateFilename(contentPage.Url);
            var title = contentPage.FrontMatter.Title;
            var description = contentPage.FrontMatter.Description ?? "";
            var date = contentPage.FrontMatter.Date.ToString("yyyy MMMM dd");

            var html = BuildSocialCardHtml(title, description, date, png);

            await page.SetContentAsync(html);
            await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

            var imageBytes = await page.ScreenshotAsync(new PageScreenshotOptions { Type = ScreenshotType.Png });

            contentToCreate.Add(new ContentToCreate($"social-images/{filename}", imageBytes));
        }

        return contentToCreate.ToImmutableList();
    }

    private static string GenerateFilename(string url)
    {
        var sanitized = url.Replace("/", "-").Replace("\\", "-").Trim('-');
        return string.IsNullOrEmpty(sanitized) ? "index.png" : $"{sanitized}.png";
    }

    static string ConvertPngToBase64ImgTag(string imagePath)
    {
        if (!File.Exists(imagePath))
            throw new FileNotFoundException("Image file not found.", imagePath);

        byte[] imageBytes = File.ReadAllBytes(imagePath);
        string base64String = Convert.ToBase64String(imageBytes);
        return $"data:image/png;base64,{base64String}";
    }
    
    private static string BuildSocialCardHtml(string title, string description, string date, string backgroundImage)
    {
        
        
        return $$$"""
                  <!DOCTYPE html>
                  <html>
                  <head>
                  <link rel="preconnect" href="https://fonts.googleapis.com">
                  <link rel="preconnect" href="https://fonts.gstatic.com" crossorigin>
                  <link href="https://fonts.googleapis.com/css2?family=Quicksand:wght@300..700&display=swap" rel="stylesheet">
                  
                      <style>
                                 body {
                              margin: 0;
                              padding: 30px;
                              font-family: Quicksand, -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif;
                              background-color: #000000;
                              width: 100vw;
                              height: 100vh;
                              box-sizing: border-box;
                              display: flex;
                              flex-direction: column;
                              justify-content: space-between;
                              align-items: flex-start;
                              color: #fff;
                          }
                          
                          .container {
                              z-index:50;
                              width: 100%;
                              height: 100%;
                              display: flex;
                              flex-direction: column;
                              justify-content: space-between;
                              max-width: none;
                              margin: 0;
                          }
                          
                          .content-top {
                              display: flex;
                              flex-direction: column;
                              align-items: flex-start;
                              gap: clamp(15px, 3vh, 30px);
                              flex-grow: 1;
                              max-width: 85%;
                          }
                          
                          .title {
                              font-size: 48px;
                              font-weight: 900;
                              line-height: 1.3;
                              margin: 0;
                              text-shadow: #000 1px 0 10px;
                              width: 100%;
                          }
                          
                          .description {
                              font-size: 20px;
                              font-weight: 300;
                              line-height: 1.3;
                              margin: 0;
                              opacity: 0.9;
                              max-width: 70%;
                              display: -webkit-box;
                              -webkit-line-clamp: 4;
                              -webkit-box-orient: vertical;
                              overflow: hidden;
                          }
                          
                          .date {
                              font-size: clamp(12px, 2vw, 20px);
                              font-weight: 300;
                              opacity: 0.8;
                              text-transform: uppercase;
                              letter-spacing: clamp(1px, 0.2vw, 2px);
                              margin: 0;
                              align-self: flex-start;
                          }
                          
                  .image {
                    position: absolute;
                    z-index:10;
                    width:100vw;
                    top:0;
                    left:0;
                    height: 100vh; /* Full screen height */
                    background-color: #1e1e1e; /* Fallback background */
                    overflow: hidden;
                  }

                  .gradient-overlay {
                    position: absolute;
                    top: 0;
                    left: 0;
                    width: 50vw; /* Adjust how far the gradient extends */
                    height: 100%;
                    background: linear-gradient(to right, #1e1e1e 0%, #1e1e1e 50%, transparent 100%);
                    z-index: 99;
                  }

                  .image img {
                    position: absolute;
                    right:0;
                    top:0;
                    height:100vh;
                    z-index: -1;
                    scale:1.25
                  }
                  </style>
                  </head>
                  <body>
                      <div class="container">
                          <div class="content-top">
                              <div class="title">{{{title}}}</div>
                              <div class="description">{{{description}}}</div>
                          </div>
                          <div class="date">{{{date}}}</div>
                      </div>
                  <div class="image">
                    <img src="{{{backgroundImage}}}"/>
                      <div class="gradient-overlay"></div>

                  </div>
                  </body>
                  </html>
                  """;
    }

    public int SearchPriority { get; } = 0;
}