using System.Collections.Immutable;
using System.ServiceModel.Syndication;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using BlazorStatic.Models;

namespace BlazorStatic.Services;

/// <summary>
/// Service for generating sitemap.xml and RSS feed files for a BlazorStatic website.
/// </summary>
internal class SitemapRssService
{
    private readonly BlazorStaticOptions _options;
    private readonly IEnumerable<IBlazorStaticContentService> _contentServices;

    /// <summary>
    /// Initializes a new instance of the <see cref="SitemapRssService"/> class.
    /// </summary>
    /// <param name="options">The BlazorStatic options.</param>
    /// <param name="contentServices">The collection of content services.</param>
    public SitemapRssService(
        BlazorStaticOptions options,
        IEnumerable<IBlazorStaticContentService> contentServices 
        )
    {
        _options = options;
        _contentServices = contentServices;
    }

    /// <summary>
    /// Generates a sitemap.xml file.
    /// </summary>
    /// <returns>The XML string representation of the sitemap.</returns>
    public string GenerateSitemap()
    {
        var baseUrl = GetBaseUrl().TrimEnd('/');
        
        // Create the sitemap root element
        XNamespace ns = "https://www.sitemaps.org/schemas/sitemap/0.9";
        var root = new XElement(ns + "urlset");
        
        // Collect all pages from content services
        var pagesToGenerate = _contentServices
            .Aggregate(ImmutableList<PageToGenerate>.Empty, 
                (current, item) => current.AddRange(item.GetPagesToGenerate()))
            .AddRange(_options.PagesToGenerate);
        
        // Add each page to the sitemap
        foreach (var page in pagesToGenerate)
        {
            var urlElement = new XElement(ns + "url",
                new XElement(ns + "loc", $"{baseUrl}/{page.Url.TrimStart('/')}"));
            
            // Add lastmod if available
            var metadata = page.Metadata;
            if (metadata == null) continue;
            
            if (metadata.LastMod != null)
            {
                urlElement.Add(new XElement(ns + "lastmod", 
                    metadata.LastMod.Value.ToString("yyyy-MM-dd")));
            }
            
            root.Add(urlElement);
        }
        
        // Create XML document
        var document = new XDocument(new XDeclaration("1.0", "utf-8", null), root);
        
        // Return the XML as a string
        var sb = new StringBuilder();
        using (var writer = XmlWriter.Create(sb, new XmlWriterSettings { Indent = true }))
        {
            document.Save(writer);
        }
        
        return sb.ToString();
    }
    
    /// <summary>
    /// Generates an RSS feed XML file.
    /// </summary>
    /// <returns>The XML string representation of the RSS feed.</returns>
    public string GenerateRssFeed()
    {
        var baseUrl = GetBaseUrl().TrimEnd('/');
        
        // Create the feed
        var feed = new SyndicationFeed(
            _options.BlogTitle,
            _options.BlogDescription,
            new Uri(baseUrl))
        {
            Language = "en-us",
            LastUpdatedTime = DateTimeOffset.UtcNow
        };
        
        // Collect items for the RSS feed
        var items = new List<SyndicationItem>();
        
        // Go through all content services to find posts that should be in the RSS feed
        foreach (var contentService in _contentServices)
        {
            var pages = contentService.GetPagesToGenerate()
                .Where(p => p.Metadata?.RssItem == true)
                .ToList();

            var syndicationItems = pages
                .Where(page => page.Metadata != null && !string.IsNullOrEmpty(page.Metadata?.Title))
                .Select(page => GetSyndicationItem(page.Url, page.Metadata!, baseUrl));
            items.AddRange(syndicationItems);
        }
        
        feed.Items = items;
        
        // Write the feed to a string
        var sb = new StringBuilder();
        using (var writer = XmlWriter.Create(sb, new XmlWriterSettings { Indent = true }))
        {
            var formatter = new Rss20FeedFormatter(feed);
            formatter.WriteTo(writer);
        }
        
        return sb.ToString();
    }

    private static SyndicationItem GetSyndicationItem(string url, Metadata metadata, string baseUrl)
    {
        
        return new SyndicationItem(metadata.Title, metadata.Description ?? string.Empty, new Uri($"{baseUrl}/{url.TrimStart('/')}"), url, metadata.LastMod.HasValue
            ? new DateTimeOffset(metadata.LastMod.Value)
            : DateTimeOffset.UtcNow);
    }

    private string GetBaseUrl()
    {
        // First, try to get it from options
        if (!string.IsNullOrEmpty(_options.BaseUrl))
        {
            return _options.BaseUrl;
        }
        
        return "http://example.com";
    }
}