namespace BlazorStatic.Services;

/// <summary>
/// Base exception class for all BlazorStatic-specific exceptions.
/// </summary>
public class BlazorStaticException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="BlazorStaticException"/> class.
    /// </summary>
    public BlazorStaticException() : base("An error occurred in BlazorStatic.") { }

    /// <summary>
    /// Initializes a new instance of the <see cref="BlazorStaticException"/> class with a specified error message.
    /// </summary>
    /// <param name="message">The error message that explains the reason for the exception.</param>
    protected BlazorStaticException(string message) : base(message) { }

    /// <summary>
    /// Initializes a new instance of the <see cref="BlazorStaticException"/> class with a specified error message
    /// and a reference to the inner exception that is the cause of this exception.
    /// </summary>
    /// <param name="message">The error message that explains the reason for the exception.</param>
    /// <param name="innerException">The exception that is the cause of the current exception.</param>
    public BlazorStaticException(string message, Exception innerException) : base(message, innerException) { }
}

/// <summary>
/// Exception thrown when there is an error processing content in BlazorStatic.
/// </summary>
public class ContentProcessingException : BlazorStaticException
{
    /// <summary>
    /// Gets the path to the content file that caused the exception.
    /// </summary>
    public string? ContentPath { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="ContentProcessingException"/> class.
    /// </summary>
    public ContentProcessingException() : base("An error occurred while processing content.") { }

    /// <summary>
    /// Initializes a new instance of the <see cref="ContentProcessingException"/> class with a specified error message.
    /// </summary>
    /// <param name="message">The error message that explains the reason for the exception.</param>
    protected ContentProcessingException(string message) : base(message) { }

    /// <summary>
    /// Initializes a new instance of the <see cref="ContentProcessingException"/> class with a specified error message
    /// and the path to the content file that caused the exception.
    /// </summary>
    /// <param name="message">The error message that explains the reason for the exception.</param>
    /// <param name="contentPath">The path to the content file that caused the exception.</param>
    protected ContentProcessingException(string message, string contentPath)
        : base($"{message} Content path: {contentPath}")
    {
        ContentPath = contentPath;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ContentProcessingException"/> class with a specified error message,
    /// the path to the content file that caused the exception, and a reference to the inner exception.
    /// </summary>
    /// <param name="message">The error message that explains the reason for the exception.</param>
    /// <param name="contentPath">The path to the content file that caused the exception.</param>
    /// <param name="innerException">The exception that is the cause of the current exception.</param>
    public ContentProcessingException(string message, string contentPath, Exception innerException)
        : base($"{message} Content path: {contentPath}", innerException)
    {
        ContentPath = contentPath;
    }

    /// <inheritdoc />
    public ContentProcessingException(string message, Exception innerException) : base(message, innerException)
    {
    }
}

/// <summary>
/// Exception thrown when there is an error with file operations in BlazorStatic.
/// </summary>
public class FileOperationException : BlazorStaticException
{
    /// <summary>
    /// Gets the path to the file that caused the exception.
    /// </summary>
    public string? FilePath { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="FileOperationException"/> class.
    /// </summary>
    public FileOperationException() : base("An error occurred during a file operation.") { }

    /// <summary>
    /// Initializes a new instance of the <see cref="FileOperationException"/> class with a specified error message.
    /// </summary>
    /// <param name="message">The error message that explains the reason for the exception.</param>
    public FileOperationException(string message) : base(message) { }

    /// <summary>
    /// Initializes a new instance of the <see cref="FileOperationException"/> class with a specified error message
    /// and the path to the file that caused the exception.
    /// </summary>
    /// <param name="message">The error message that explains the reason for the exception.</param>
    /// <param name="filePath">The path to the file that caused the exception.</param>
    public FileOperationException(string message, string filePath)
        : base($"{message} File path: {filePath}")
    {
        FilePath = filePath;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="FileOperationException"/> class with a specified error message,
    /// the path to the file that caused the exception, and a reference to the inner exception.
    /// </summary>
    /// <param name="message">The error message that explains the reason for the exception.</param>
    /// <param name="filePath">The path to the file that caused the exception.</param>
    /// <param name="innerException">The exception that is the cause of the current exception.</param>
    public FileOperationException(string message, string filePath, Exception innerException)
        : base($"{message} File path: {filePath}", innerException)
    {
        FilePath = filePath;
    }

    /// <inheritdoc />
    public FileOperationException(string message, Exception innerException) : base(message, innerException)
    {
    }
}