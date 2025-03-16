namespace BlazorStatic.Models;

/// <summary>
///     Class for keeping the page to generate properties together.
/// </summary>
/// <param name="Url"></param>
/// <param name="OutputFile"></param>
/// <param name="Metadata">Additional file properties.</param>
public record PageToGenerate(string Url, string OutputFile, Metadata? Metadata = null);