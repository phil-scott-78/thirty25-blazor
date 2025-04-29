namespace BlazorStatic.Services.Infrastructure;

/// <summary>
/// Provides utilities for working with paths and URLs in BlazorStatic.
/// </summary>
public static class PathUtilities
{
    /// <summary>
    /// Converts a file path to a URL-friendly path.
    /// </summary>
    /// <param name="filePath">The file path to convert.</param>
    /// <param name="baseContentPath">The base content path to make the path relative to.</param>
    /// <returns>A URL-friendly relative path.</returns>
    public static string FilePathToUrlPath(string filePath, string baseContentPath)
    {
        var relativePath = Path.GetRelativePath(baseContentPath, filePath);
        var directoryPath = Path.GetDirectoryName(relativePath) ?? string.Empty;
        var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(relativePath).Slugify();

        return Path.Combine(directoryPath, fileNameWithoutExtension)
            .Replace(Path.DirectorySeparatorChar, '/');
    }

    /// <summary>
    /// Combines a base URL with a relative path to create a complete URL.
    /// </summary>
    /// <param name="baseUrl">The base URL.</param>
    /// <param name="relativePath">The relative path to append.</param>
    /// <returns>A complete URL.</returns>
    public static string CombineUrl(string baseUrl, string relativePath)
    {
        baseUrl = baseUrl.Trim('/');
        relativePath = relativePath.Trim('/');

        return $"{baseUrl}/{relativePath}";
    }

    /// <summary>
    /// Gets all files matching a pattern in a directory.
    /// </summary>
    /// <param name="directoryPath">The directory to search.</param>
    /// <param name="pattern">The file pattern to match.</param>
    /// <param name="recursive">Whether to search subdirectories.</param>
    /// <returns>A tuple with the array of file paths and the absolute directory path.</returns>
    public static (string[] Files, string AbsolutePath) GetFilesInDirectory(
        string directoryPath,
        string pattern,
        bool recursive = true)
    {
        // Configure enumeration options for directory search
        EnumerationOptions enumerationOptions = new()
        {
            IgnoreInaccessible = true,
            RecurseSubdirectories = recursive
        };

        // Get all files matching the pattern and return with the content path
        return (Directory.GetFiles(directoryPath, pattern, enumerationOptions), directoryPath);
    }

    /// <summary>
    /// Validates a path and ensures it exists.
    /// </summary>
    /// <param name="path">The path to validate.</param>
    /// <param name="createIfNotExists">Whether to create the directory if it doesn't exist.</param>
    /// <returns>The absolute path.</returns>
    /// <exception cref="DirectoryNotFoundException">Thrown when the directory doesn't exist and createIfNotExists is false.</exception>
    public static string ValidateDirectoryPath(string path, bool createIfNotExists = false)
    {
        var fullPath = Path.GetFullPath(path);

        if (Directory.Exists(fullPath)) return fullPath;

        if (createIfNotExists)
        {
            Directory.CreateDirectory(fullPath);
        }
        else
        {
            throw new DirectoryNotFoundException($"Directory not found: {fullPath}");
        }

        return fullPath;
    }
}