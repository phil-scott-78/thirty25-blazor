﻿using MyLittleContentEngine.Services.Content.MarkdigExtensions.CodeHighlighting;
using MyLittleContentEngine.Services.Content.MarkdigExtensions.Tabs;

namespace MyLittleContentEngine.Services.Content.Roslyn;

/// <summary>
/// Configuration options for the Roslyn syntax highlighting service.
/// </summary>
/// <remarks>
/// This class provides configuration settings for the <see cref="RoslynHighlighterService"/>
/// to specify the paths required for syntax highlighting of code blocks in Markdown content.
/// </remarks>
public record RoslynHighlighterOptions
{
    /// <summary>
    /// Gets or initializes the solutions to connect the <see cref="RoslynHighlighterService"/> to for highlighting.
    /// </summary>
    public ConnectedDotNetSolution? ConnectedSolution { get; init; }

    public Func<CodeHighlightRenderOptions> CodeHighlightRenderOptionsFactory { get; init; } = () => CodeHighlightRenderOptions.MonorailColorful;
    public Func<TabbedCodeBlockRenderOptions> TabbedCodeBlockRenderOptionsFactory { get; init; } = () => TabbedCodeBlockRenderOptions.MonorailColorful;

}

/// <summary>
/// Solution connected to the <see cref="RoslynHighlighterOptions"/>.
/// </summary>
public record ConnectedDotNetSolution
{
    /// <summary>
    /// Gets or sets the path to the solution file (.sln) that contains the projects
    /// to be used for syntax highlighting.
    /// </summary>
    /// <remarks>
    /// The path can be absolute or relative to the application's execution directory.
    /// </remarks>
    public required string SolutionPath { get; init; }

    /// <summary>
    /// Gets or sets the path to the directory containing the projects
    /// that will be used for syntax highlighting.
    /// </summary>
    /// <remarks>
    /// The path can be absolute or relative to the application's execution directory.
    /// This directory should contain the C# projects referenced in code examples.
    /// </remarks>
    public required string ProjectsPath { get; init; }
}