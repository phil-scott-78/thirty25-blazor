using BlazorStatic.Models;

namespace Thirty25.Web;

public class FrontMatter : IFrontMatter, IFrontMatterWithTags
{
    /// <summary>Title of the blog post.</summary>
    public string Title { get; set; } = "Empty title";

    public string Description { get; set; } = string.Empty;
    
    public string Repository { get; set; } = string.Empty;
    
    /// <summary>Date of publishing the blog post.</summary>
    public DateTime Date { get; set; } = DateTime.Now;

    /// <inheritdoc />
    public bool IsDraft { get; set; }

    public List<string> Tags { get; set; } = [];
    
    public Metadata AsMetadata()
    {
        return new Metadata()
        {
            Title = Title,
            Description = Description,
            LastMod = Date,
            RssItem = true
        };
    }
}