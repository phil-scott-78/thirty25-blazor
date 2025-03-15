namespace BlazorStatic.Services;

internal interface IContentPostService
{
    /// <summary>
    /// Parses all content defined in the content service.
    /// </summary>
    void ParseAndAddPosts();
}