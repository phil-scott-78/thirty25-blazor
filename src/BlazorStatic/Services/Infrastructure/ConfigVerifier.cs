using Microsoft.Extensions.Logging;

namespace BlazorStatic.Services.Infrastructure;

/// <summary>
/// Verifies the validity of BlazorStatic configuration options.
/// </summary>
/// <remarks>
/// <para>
/// This service validates paths, URLs, and other critical configuration settings
/// to ensure they meet requirements and actually exist on the file system before
/// static site generation begins.
/// </para>
/// </remarks>
internal class ConfigVerifier
{
    private readonly BlazorStaticOptions _options;
    private readonly IEnumerable<IBlazorStaticContentOptions> _contentOptionsList;
    private readonly ILogger<ConfigVerifier> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ConfigVerifier"/> class.
    /// </summary>
    /// <param name="options">The BlazorStatic general options.</param>
    /// <param name="contentOptionsList">A collection of content-specific options.</param>
    /// <param name="logger">The logger instance.</param>
    public ConfigVerifier(
        BlazorStaticOptions options,
        IEnumerable<IBlazorStaticContentOptions> contentOptionsList,
        ILogger<ConfigVerifier> logger)
    {
        _options = options;
        _contentOptionsList = contentOptionsList;
        _logger = logger;
    }

    /// <summary>
    /// Verifies the configuration and logs any issues found.
    /// </summary>
    /// <returns>True if the configuration passes all validation checks; otherwise, false.</returns>
    public bool VerifyConfiguration()
    {
        var issues = new List<string>();

        // Verify BlazorStaticOptions
        VerifyBlazorStaticOptions(issues);

        // Verify each IBlazorStaticContentOptions instance
        foreach (var contentOptions in _contentOptionsList)
        {
            VerifyContentOptions(contentOptions, issues);
        }

        // Log all issues and return result
        if (issues.Count > 0)
        {
            _logger.LogWarning("Configuration verification found {IssueCount} issues:", issues.Count);
            foreach (var issue in issues)
            {
                _logger.LogWarning("- {Issue}", issue);
            }

            return false;
        }

        _logger.LogInformation("Configuration verification passed.");
        return true;
    }

    private void VerifyBlazorStaticOptions(List<string> issues)
    {
        // Verify BaseUrl
        if (string.IsNullOrWhiteSpace(_options.BaseUrl))
        {
            issues.Add("BaseUrl is empty or whitespace");
        }
        else if (!Uri.TryCreate(_options.BaseUrl, UriKind.Absolute, out _))
        {
            issues.Add($"BaseUrl '{_options.BaseUrl}' is not a valid absolute URI");
        }
        else if (_options.BaseUrl.EndsWith("/"))
        {
            issues.Add($"BaseUrl '{_options.BaseUrl}' should not end with a trailing slash");
        }

        // Verify OutputFolderPath
        if (string.IsNullOrWhiteSpace(_options.OutputFolderPath))
        {
            issues.Add("OutputFolderPath is empty or whitespace");
        }

        // Verify required string properties
        if (string.IsNullOrWhiteSpace(_options.BlogTitle))
        {
            issues.Add("BlogTitle is empty or whitespace");
        }

        if (string.IsNullOrWhiteSpace(_options.BlogDescription))
        {
            issues.Add("BlogDescription is empty or whitespace");
        }
    }

    private static void VerifyContentOptions(IBlazorStaticContentOptions contentOptions, List<string> issues)
    {
        // Verify ContentPath
        if (string.IsNullOrWhiteSpace(contentOptions.ContentPath))
        {
            issues.Add($"ContentPath for {contentOptions.GetType().Name} is empty or whitespace");
        }
        else
        {
            var contentPathFull = Path.Combine(Directory.GetCurrentDirectory(), contentOptions.ContentPath);
            if (!Directory.Exists(contentPathFull))
            {
                issues.Add($"ContentPath '{contentPathFull}' does not exist");
            }
        }

        // Verify PageUrl
        if (string.IsNullOrWhiteSpace(contentOptions.PageUrl))
        {
            issues.Add($"PageUrl for {contentOptions.GetType().Name} is empty or whitespace");
        }
        else if (contentOptions.PageUrl.Contains('/'))
        {
            issues.Add($"PageUrl '{contentOptions.PageUrl}' contains forward slashes, which is not allowed");
        }

        // Type-specific validations for BlazorStaticContentOptions<T>
        var contentOptionsType = contentOptions.GetType();

        // Using reflection to check the PostFilePattern property
        if (contentOptionsType.GetProperty("PostFilePattern") is { } postFilePatternProperty)
        {
            var postFilePattern = postFilePatternProperty.GetValue(contentOptions) as string;
            if (string.IsNullOrWhiteSpace(postFilePattern))
            {
                issues.Add($"PostFilePattern for {contentOptions.GetType().Name} is empty or whitespace");
            }
        }

        // Using reflection to check TagsOptions property
        if (contentOptionsType.GetProperty("Tags") is { } tagsProperty)
        {
            var tagsOptions = tagsProperty.GetValue(contentOptions);
            if (tagsOptions == null)
            {
                issues.Add($"Tags options for {contentOptions.GetType().Name} is null");
            }
            else
            {
                var tagsOptionsType = tagsOptions.GetType();

                // Check TagsPageUrl
                if (tagsOptionsType.GetProperty("TagsPageUrl") is { } tagsPageUrlProperty)
                {
                    var tagsPageUrl = tagsPageUrlProperty.GetValue(tagsOptions) as string;
                    if (string.IsNullOrWhiteSpace(tagsPageUrl))
                    {
                        issues.Add(
                            $"TagsPageUrl in Tags for {contentOptions.GetType().Name} is empty or whitespace");
                    }
                    else if (tagsPageUrl.Contains("/"))
                    {
                        issues.Add($"TagsPageUrl '{tagsPageUrl}' contains forward slashes, which is not allowed");
                    }
                }

                // Check TagEncodeFunc
                if (tagsOptionsType.GetProperty("TagEncodeFunc") is { } tagEncodeFuncProperty)
                {
                    var tagEncodeFunc = tagEncodeFuncProperty.GetValue(tagsOptions);
                    if (tagEncodeFunc == null)
                    {
                        issues.Add($"TagEncodeFunc in Tags for {contentOptions.GetType().Name} is null");
                    }
                }
            }
        }
    }
}