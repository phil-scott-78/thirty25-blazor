namespace BlazorStatic.Services.Content.MarkdigExtensions.CodeHighlighting;

/// <summary>
/// Options for customizing the CSS classes used in the code highlight renderer
/// </summary>
public record CodeHighlightRenderOptions
{
    /// <summary>
    /// Gets the default <see cref="CodeHighlightRenderOptions"/>.
    /// </summary>
    public static readonly CodeHighlightRenderOptions Default = new()
    {
        OuterWrapperCss = "",
        StandaloneContainerCss = "",
        PreBaseCss = "",
        PreStandaloneCss = ""
    };

    /// <summary>
    /// CSS class for the outer wrapper element
    /// </summary>
    public required string OuterWrapperCss { get; init; }

    /// <summary>
    /// CSS classes for the container when not in a tabbed code block
    /// </summary>
    public required string StandaloneContainerCss { get; init; }

    /// <summary>
    /// CSS classes for the Pre element
    /// </summary>
    public required string PreBaseCss { get; init; }

    /// <summary>
    /// Additional CSS classes for the Pre element when not in a tabbed code block
    /// </summary>
    public required string PreStandaloneCss { get; init; }
}