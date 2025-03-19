# Thirty25, Phil Scott's Blog

Based heavily on BlazorStatic,  a library that enables you to generate static HTML websites from Blazor applications. This approach combines the development experience of Blazor with the performance and deployment simplicity of static websites.

## Features

- Convert server-side rendered Blazor applications to static HTML, CSS, and JavaScript
- Support for Markdown content with YAML front matter
- Automatic blog post and tag page generation
- Customizable content processing pipeline
- File watching and hot reload during development
- Sitemap.xml and RSS feed generation
- Flexible route-based static site generation

## Getting Started

You're gonna have to fork this repo and rip my stuff out to use this. For a better supported and complete experience, check out [BlazorStatic](https://github.com/BlazorStatic/BlazorStatic).

### Basic Configuration

Configure BlazorStatic in your `Program.cs` file:

```csharp
using BlazorStatic;
using YourNamespace.Web.BlogServices;
using YourNamespace.Web.Components;

var builder = WebApplication.CreateBuilder(args);

builder.WebHost.UseStaticWebAssets();
builder.Services.AddRazorComponents();

// Configure site-wide settings
builder.Services.AddBlazorStaticService(() => new BlazorStaticOptions
{
    BlogTitle = "Your Blog Title",
    BlogDescription = "Your blog description goes here",
    BaseUrl = "https://yourdomain.com"
});

// Configure content service with custom front matter type
builder.Services.AddBlazorStaticContentService(() => new BlazorStaticContentOptions<FrontMatter>
{
    PageUrl = "blog",
    ContentPath = "blog/content"
});

var app = builder.Build();
app.UseHttpsRedirection();
app.MapRazorComponents<App>();

// Run the app or generate static files based on command-line arguments
await app.RunOrBuildBlazorStaticSite(args);
```

## Configuration Options

### BlazorStaticOptions

Site-wide configuration options for the static generation process:

| Property | Description | Default |
|----------|-------------|---------|
| `BlogTitle` | Title of the blog or website | *required* |
| `BlogDescription` | Description or tagline of the blog | *required* |
| `BaseUrl` | Base URL for the published site (e.g., "https://example.com") | *required* |
| `OutputFolderPath` | Output directory path for generated static files | "output" |
| `PagesToGenerate` | Collection of pages to generate as static HTML files | empty list |
| `AddPagesWithoutParameters` | Whether to include non-parameterized Razor pages | true |
| `IndexPageHtml` | Filename to use for index pages | "index.html" |
| `IgnoredPathsOnContentCopy` | Paths to exclude when copying content | empty list |
| `FrontMatterDeserializer` | YAML deserializer for parsing front matter | default config |
| `MarkdownPipeline` | Markdown processing pipeline | default config |

### BlazorStaticContentOptions<TFrontMatter>

Content-specific configuration options:

| Property | Description | Default |
|----------|-------------|---------|
| `ContentPath` | Folder path where content files are stored | "Content/Blog" |
| `PostFilePattern` | File pattern used to identify content files | "*.md" |
| `PageUrl` | URL path component for the page that displays content | "blog" |
| `PreProcessMarkdown` | Hook to process markdown content before rendering | identity function |
| `PostProcessMarkdown` | Hook to process front matter and HTML after parsing | identity function |
| `ExcludedMapRoutes` | List of routes to exclude from static generation | empty list |
| `Tags` | Options related to tag functionality | default config |

### Tags Options

Tag-related configuration options:

| Property | Description | Default |
|----------|-------------|---------|
| `AddTagPagesFromPosts` | Whether to generate tag pages from blog posts | true |
| `TagsPageUrl` | URL path component for the tag page | "tags" |
| `TagEncodeFunc` | Function to encode tag strings into URL-friendly formats | Slugify function |

## Content Structure

### Front Matter

Create a class to define the structure of your markdown front matter:

```csharp
public class FrontMatter : IFrontMatter, IFrontMatterWithTags
{
    public required string Title { get; set; }
    public string Description { get; set; } = string.Empty;
    public DateTime Date { get; set; } = DateTime.Now;
    public bool IsDraft { get; set; }
    public List<string> Tags { get; set; } = new();
}
```

### Markdown Files

Create markdown files with YAML front matter in your content directory:

```markdown
---
title: My First Post
description: A brief description of this post
date: 2023-01-01
tags: [blazor, static, web]
---

# Hello World

This is my first blog post using BlazorStatic.
```

## Building Your Static Site

To generate a static site from your Blazor application, run:

```bash
dotnet run -- build
```

This will create static HTML files in the configured output directory.

## Advanced Usage

### Custom Content Processing

You can customize how markdown content is processed:

```csharp
builder.Services.AddBlazorStaticContentService(() => new BlazorStaticContentOptions<FrontMatter>
{
    PageUrl = "blog",
    ContentPath = "blog/content",
    PreProcessMarkdown = (serviceProvider, markdownContent) => {
        // Modify markdown content before parsing
        return markdownContent;
    },
    PostProcessMarkdown = (serviceProvider, frontMatter, htmlContent) => {
        // Modify front matter or HTML content after parsing
        return (frontMatter, htmlContent);
    }
});
```

### Custom Page Generation

Define specific pages to generate:

```csharp
builder.Services.AddBlazorStaticService(() => new BlazorStaticOptions
{
    // Required properties
    BlogTitle = "My Blog",
    BlogDescription = "Blog description",
    BaseUrl = "https://example.com",
    
    // Add custom pages
    PagesToGenerate = ImmutableList.Create(
        new PageToGenerate("/custom-page", "custom-page.html"),
        new PageToGenerate("/projects/featured", "projects/featured.html")
    )
});
```

### Customizing Ignored Paths

Specify paths to ignore during content copying:

```csharp
builder.Services.AddBlazorStaticService(() => new BlazorStaticOptions
{
    // Required properties
    BlogTitle = "My Blog",
    BlogDescription = "Blog description",
    BaseUrl = "https://example.com",
    
    // Ignore specific paths
    IgnoredPathsOnContentCopy = ImmutableList.Create(
        "draft-content",
        "temp-files"
    )
});
```

## Components

BlazorStatic includes several Razor components to display blog content:

- `BlogPost.razor` - Displays a single blog post
- `BlogPostsList.razor` - Displays a list of blog posts
- `BlogSummary.razor` - Displays a summary of a blog post

## License

MIT License

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.