---
title: "Creating Your First Site"
description: "Build a complete content site from scratch using MyLittleContentEngine"
order: 1
tags:
  - tutorial
  - getting-started
  - setup
---

In this tutorial, you'll create your first content site using MyLittleContentEngine. By the end, you'll have a working Blazor application that can serve markdown content and generate static HTML files.

## What You'll Build

You'll create a simple blog-style website with:
- A home page displaying recent posts
- Individual blog post pages
- Tag-based categorization
- Static HTML generation for deployment

## Prerequisites

Before starting, ensure you have:
- .NET 8 SDK or later installed
- A code editor (Visual Studio, VS Code, or JetBrains Rider)
- Basic knowledge of C# and web development
- Familiarity with command-line tools

## Step 1: Create a New Blazor Project

Start by creating a new Blazor Server project:

```bash
dotnet new blazor-server -n MyFirstContentSite
cd MyFirstContentSite
```

## Step 2: Add MyLittleContentEngine

Add the NuGet package reference to your project:

```bash
dotnet add package MyLittleContentEngine
```

## Step 3: Create Your Front Matter Model

Create a model to define the structure of your blog post metadata. Add a new file `BlogPost.cs`:

```csharp
using MyLittleContentEngine.Models;

namespace MyFirstContentSite;

public class BlogPost : IFrontMatter
{
    public required string Title { get; set; }
    public string? Description { get; set; }
    public DateTime Date { get; set; }
    public List<string> Tags { get; set; } = new();
    public bool IsDraft { get; set; }
}
```

## Step 4: Configure the Content Engine

Open `Program.cs` and configure MyLittleContentEngine. Replace the existing content with:

```csharp
using MyFirstContentSite;
using MyLittleContentEngine;

var builder = WebApplication.CreateBuilder(args);

// Add Blazor services
builder.Services.AddRazorPages();
builder.Services.AddServerSideBlazor();

// Configure MyLittleContentEngine
builder.Services.AddContentEngineService(() => new ContentEngineOptions
{
    BlogTitle = "My First Content Site",
    BlogDescription = "Built with MyLittleContentEngine",
    BaseUrl = "https://mysite.com", // Change to your domain
    ContentRootPath = "Content"
});

// Add content service with your front matter type
builder.Services.AddContentEngineStaticContentService<BlogPost>();

var app = builder.Build();

// Configure the pipeline
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();

app.MapRazorPages();
app.MapBlazorHub();
app.MapFallbackToPage("/_Host");

// Run or build based on command line arguments
await app.RunOrBuildContent(args);
```

## Step 5: Create the Content Structure

Create the content directory structure:

```bash
mkdir -p Content/Blog/2025/01
```

## Step 6: Write Your First Blog Post

Create your first blog post at `Content/Blog/2025/01/welcome.md`:

`````````markdown
---
title: "Welcome to My Content Site"
description: "Getting started with MyLittleContentEngine"
date: 2025-01-15
tags:
  - introduction
  - welcome
isDraft: false
---

Welcome to my new content site! This is my first post using MyLittleContentEngine.

## What is MyLittleContentEngine?

MyLittleContentEngine is a static site generator built specifically for .NET Blazor applications. It allows you to:

- Write content in Markdown
- Use strongly-typed front matter
- Generate static HTML for fast loading
- Take advantage of the entire .NET ecosystem

## Getting Started

Creating content is as simple as writing Markdown files with YAML front matter. The engine handles the rest!

```csharp
// Example: Strongly-typed front matter
public class BlogPost : IFrontMatter
{
    public required string Title { get; set; }
    public DateTime Date { get; set; }
    // ... other properties
}
```

Stay tuned for more posts about building amazing content sites with .NET!
`````````

## Step 7: Create Your Layout

Create `Pages/Shared/_Layout.cshtml` (if it doesn't exist) or update `Pages/_Host.cshtml` to include basic styling and navigation:

```html
<!DOCTYPE html>
<html lang="en">
<head>
    <meta charset="utf-8" />
    <meta name="viewport" content="width=device-width, initial-scale=1.0" />
    <title>My First Content Site</title>
    <base href="~/" />
    <link href="css/bootstrap/bootstrap.min.css" rel="stylesheet" />
    <link href="css/site.css" rel="stylesheet" />
</head>
<body>
    <div class="container">
        <header class="border-bottom mb-4">
            <h1><a href="/" class="text-decoration-none">My First Content Site</a></h1>
            <p class="text-muted">Built with MyLittleContentEngine</p>
        </header>
        
        <main>
            @RenderBody()
        </main>
        
        <footer class="border-top mt-5 pt-3 text-muted">
            <p>&copy; 2025 My First Content Site. Powered by MyLittleContentEngine.</p>
        </footer>
    </div>

    <script src="_framework/blazor.server.js"></script>
</body>
</html>
```

## Step 8: Create a Home Page

Create `Pages/Index.razor` to display your blog posts:

```razor
@page "/"
@using MyLittleContentEngine.Services.Content
@inject IContentService<BlogPost> ContentService

<h2>Recent Posts</h2>

@if (posts != null && posts.Any())
{
    <div class="row">
        @foreach (var post in posts.Take(5))
        {
            <div class="col-md-12 mb-4">
                <div class="card">
                    <div class="card-body">
                        <h5 class="card-title">
                            <a href="@post.Url" class="text-decoration-none">@post.FrontMatter.Title</a>
                        </h5>
                        @if (!string.IsNullOrEmpty(post.FrontMatter.Description))
                        {
                            <p class="card-text">@post.FrontMatter.Description</p>
                        }
                        <small class="text-muted">@post.FrontMatter.Date.ToString("MMMM dd, yyyy")</small>
                        @if (post.FrontMatter.Tags.Any())
                        {
                            <div class="mt-2">
                                @foreach (var tag in post.FrontMatter.Tags)
                                {
                                    <span class="badge bg-secondary me-1">@tag</span>
                                }
                            </div>
                        }
                    </div>
                </div>
            </div>
        }
    </div>
}
else
{
    <p>No posts yet. Create your first post in the Content/Blog directory!</p>
}

@code {
    private IEnumerable<MarkdownContentPage<BlogPost>>? posts;

    protected override async Task OnInitializedAsync()
    {
        var allPosts = await ContentService.GetAllAsync();
        posts = allPosts
            .Where(p => !p.FrontMatter.IsDraft)
            .OrderByDescending(p => p.FrontMatter.Date);
    }
}
```

## Step 9: Test Your Site

Run your site in development mode:

```bash
dotnet run
```

Navigate to `https://localhost:5001` (or the URL shown in your terminal) to see your site in action!

## Step 10: Generate Static Files

To generate static HTML files for deployment:

```bash
dotnet run build
```

This creates a `wwwroot` directory with all the static files needed to deploy your site to any web server.

## What You've Accomplished

Congratulations! You've successfully:

1. ✅ Created a new Blazor project with MyLittleContentEngine
2. ✅ Defined a strongly-typed front matter model
3. ✅ Configured the content engine
4. ✅ Created your first blog post
5. ✅ Built a home page that displays your content
6. ✅ Generated static HTML files

## Next Steps

Now that you have a working content site, you can:

- **[Learn more fundamentals](getting-started.md)** - Understand core concepts in depth
- **[Add more blog posts](adding-blog-posts.md)** - Expand your content
- **[Explore how-to guides](../how-to/index.md)** - Implement specific features
- **[Customize your styling](../how-to/integrate-monorailcss.md)** - Make it beautiful

## Troubleshooting

### Content not showing up?
- Check that your markdown files have proper YAML front matter
- Ensure `isDraft: false` in your front matter
- Verify the content path matches your configuration

### Build errors?
- Make sure your `BlogPost` class implements `IFrontMatter`
- Check that all required properties are set in your front matter
- Verify your content directory structure matches the expected format

### Styling issues?
- Ensure Bootstrap CSS is properly referenced
- Check that your layout file is being used
- Verify static files are being served correctly

You now have a solid foundation for building content-rich sites with MyLittleContentEngine!