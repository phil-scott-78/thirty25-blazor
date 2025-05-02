﻿namespace BlazorStatic.Services.Content.MarkdigExtensions.Tabs;

/// <summary>
/// Options for customizing the CSS classes used in the tabbed code block renderer
/// </summary>
public record TabbedCodeBlockRenderOptions
{
    /// <summary>
    /// Gets the default settings for a <see cref="TabbedCodeBlockRenderOptions"/>.
    /// </summary>
    public static readonly TabbedCodeBlockRenderOptions Default = new()
    {
        OuterWrapperCss = "",
        ContainerCss = "tab-container",
        TabListCss = "tab-list",
        TabButtonCss = "tab-button",
        TabPanelCss = "tab-panel"
    };

    /// <summary>
    /// CSS class for the outer wrapper element
    /// </summary>
    public required string OuterWrapperCss { get; init; }

    /// <summary>
    /// CSS classes for the container
    /// </summary>
    public required string ContainerCss { get; init; }

    /// <summary>
    /// CSS classes for the tab list
    /// </summary>
    public required string TabListCss { get; init; }

    /// <summary>
    /// CSS classes for the tab buttons
    /// </summary>
    public required string TabButtonCss { get; init; }

    /// <summary>
    /// CSS classes for the tab panels
    /// </summary>
    public required string TabPanelCss { get; init; }
}