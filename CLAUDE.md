# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

This is Phil Scott's blog - a static site generator built on Blazor and the MyLittleContentEngine.BlogSite package. Blog posts are written in Markdown and converted to static HTML.

## Common Commands

### Development
```bash
# Run development server with hot reload (recommended)
dotnet run --project src/Thirty25.Web/Thirty25.Web.csproj
```
Site runs at https://localhost:7075

### Build Static Site
```bash
# Generate static HTML files
dotnet run --project src/Thirty25.Web/Thirty25.Web.csproj --configuration Release -- build
```
Output goes to `src/Thirty25.Web/output/`

### Build Solution
```bash
dotnet build
```

## Writing Blog Posts

### File Location
Create markdown files in `src/Thirty25.Web/Content/blog/YYYY/MM/post-slug.md`

Example: `src/Thirty25.Web/Content/blog/2025/01/my-new-post.md`

### Front Matter Format
```yaml
---
Title: Your Post Title
description: Brief description for SEO and social cards
date: 2025-01-07
tags:
  - CSharp
  - Blazor
repository: https://github.com/phil-scott-78/thirty25-blazor/tree/main/blog-projects/2025/ProjectName  # Optional
series: "Series Name"  # Optional
---

Your markdown content starts here...
```

### Images and Media
Store images in `src/Thirty25.Web/Content/blog/media/` and reference them relatively:
```markdown
![Alt text](../media/image.png)
```

## Special Features for Code Examples

### xmldocid Syntax - Embedding Code from Projects

You can embed actual compiled C# code from the `blog-projects/` directory:

````markdown
```csharp:xmldocid
M:Namespace.ClassName.MethodName
```
````

**How it works:**
1. Create a working C# project in `blog-projects/YYYY/ProjectName/`
2. Add the project to `thirty25-blazor.sln`
3. Use the xmldocid syntax to reference methods/classes by their XML documentation ID
4. The code is extracted from the compiled assembly

**Options:**
- `bodyonly` - Show only method body
- `tabs=true` - Enable tabbed display
- `data="id"` - Add HTML data attribute

**Example:**
````markdown
```csharp:xmldocid
M:EfCoreTagging.MyApp.BasicQueryWithTag
```
````

### Finding XML Doc IDs

To find the XML Doc ID for a method:
1. Build the project in `blog-projects/`
2. The format is: `M:Namespace.ClassName.MethodName` for methods
3. Or: `T:Namespace.ClassName` for types

## blog-projects Directory

The `blog-projects/` directory contains working code examples that are:
- Included in the solution for IDE support
- Referenced in blog posts via xmldocid syntax
- Linked in post front matter via the `repository` field

When creating a new blog post with code examples:
1. Create a project in `blog-projects/YYYY/ProjectName/`
2. Add it to `thirty25-blazor.sln`
3. Reference it in your blog post markdown using xmldocid syntax
4. Link to it in front matter with the `repository` field

## Key Configuration

Main configuration is in `src/Thirty25.Web/Program.cs`:
- `SolutionPath = "../../thirty25-blazor.sln"` - Required for xmldocid to work
- `ContentPath = "Content/blog/"` - Where markdown files are located
- `BlogPath = "/blog"` - Base URL path for blog posts
