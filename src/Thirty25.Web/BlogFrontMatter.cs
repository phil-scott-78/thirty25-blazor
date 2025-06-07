using MyLittleContentEngine.Models;

namespace Thirty25.Web;

public class BlogFrontMatter : IFrontMatter
{
    /// <summary>Title of the blog post.</summary>
    public string Title { get; init; } = "Empty title";

    public string Description { get; init; } = string.Empty;

    public string Repository { get; init; } = string.Empty;

    /// <summary>Date of publishing the blog post.</summary>
    public DateTime Date { get; init; } = DateTime.Now;

    /// <inheritdoc />
    public bool IsDraft { get; init; } = false;

    public string[] Tags { get; init; } = [];

    public string Series { get; init; } = string.Empty;

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

    public string? Uid { get; init; } = null;
}