using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Http.Extensions;

namespace Thirty25.Web.BlogServices.Styling;

internal partial class CssClassCollectorMiddleware(RequestDelegate next)
{
    public async Task Invoke(HttpContext context, CssClassCollector collector, ILogger<CssClassCollectorMiddleware> logger)
    {
        var url = context.Request.GetEncodedUrl();

        if (!collector.ShouldProcess(url))
        {
            await next(context);
            return;
        }

        var originalBodyStream = context.Response.Body;

        using var memoryStream = new MemoryStream();
        context.Response.Body = memoryStream;

        await next(context); // Run the rest of the pipeline

        // Make sure the response is HTML before proceeding
        var contentType = context.Response.ContentType;
        if (string.IsNullOrEmpty(contentType) || !contentType.Contains("text/html", StringComparison.OrdinalIgnoreCase))
        {
            memoryStream.Seek(0, SeekOrigin.Begin);
            await memoryStream.CopyToAsync(originalBodyStream);
            context.Response.Body = originalBodyStream;
            return;
        }
        
        logger.LogInformation("Gathering CSS for {url}", url);

        memoryStream.Seek(0, SeekOrigin.Begin);
        var html = await new StreamReader(memoryStream).ReadToEndAsync();

        var classMatches = CssClassGatherRegex().Matches(html);
        var allClasses = classMatches
            .SelectMany(m => m.Groups[1].Value.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            .Distinct()
            .ToList();

        logger.LogInformation("Gathered {count} CSS classes", allClasses.Count());
        collector.AddClasses(url, allClasses);

        memoryStream.Seek(0, SeekOrigin.Begin);
        await memoryStream.CopyToAsync(originalBodyStream);
        context.Response.Body = originalBodyStream;
    }

    [GeneratedRegex("""class\s*=\s*["']([^"']+)["']""", RegexOptions.IgnoreCase, "en-US")]
    private static partial Regex CssClassGatherRegex();
}