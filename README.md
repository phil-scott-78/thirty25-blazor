# Thirty25, Phil Scott's Blog

A static site built on [Pennington.BlogSite](https://github.com/phil-scott-78/MyLittleContentEngine), a .NET static content generator. Blog posts are written in Markdown and converted to static HTML.

## Getting Started

This is a personal blog — you'll need to fork this and replace the content and configuration with your own. For a more general-purpose static site framework, see the underlying [MyLittleContentEngine](https://github.com/phil-scott-78/MyLittleContentEngine) project.

## Common Commands

### Development

```bash
dotnet run --project src/Thirty25.Web/Thirty25.Web.csproj
```

Site runs at https://localhost:7075 with hot reload for both Razor files and blog post Markdown.

### Build Static Site

```bash
dotnet run --project src/Thirty25.Web/Thirty25.Web.csproj --configuration Release -- build
```

Output goes to `src/Thirty25.Web/output/`.

### Build Solution

```bash
dotnet build
```

## Configuration

Main configuration is in `src/Thirty25.Web/Program.cs` via `AddBlogSite()`:

| Property | Description |
|----------|-------------|
| `SiteTitle` | Title of the site |
| `AuthorName` | Author's name |
| `Description` | Site tagline |
| `CanonicalBaseUrl` | Full base URL (also read from `CanonicalBaseHref` env var) |
| `BlogContentPath` | Folder under `Content/` where Markdown posts live |
| `BlogBaseUrl` | URL prefix for blog posts |
| `TagsPageUrl` | URL for the tags index page |
| `EnableRss` / `EnableSitemap` | Toggle RSS feed and sitemap generation |
| `ColorScheme` | Algorithmic color scheme (hue-based) |
| `HeroContent` | Homepage hero text |
| `MyWork` | Projects listed on the homepage |
| `Socials` | Social media links |

The `:symbol` code fence feature is configured separately:

```csharp
builder.Services.AddPenningtonTreeSitter(opts =>
{
    opts.ContentRoot = "../../blog-projects";
});
```

## Writing Blog Posts

### File Location

Create Markdown files in `src/Thirty25.Web/Content/blog/YYYY/MM/post-slug.md`.

### Front Matter

```yaml
---
Title: Your Post Title
description: Brief description for SEO and social cards
date: 2025-01-07
tags:
  - CSharp
  - Blazor
repository: https://github.com/phil-scott-78/thirty25-blazor/tree/main/blog-projects/2025/ProjectName
series: "Series Name"
---
```

### Images

Store images in `src/Thirty25.Web/Content/blog/media/` and reference them relatively:

```markdown
![Alt text](../media/image.png)
```

## Embedding Code from blog-projects

The `:symbol` fence type embeds live source code from the `blog-projects/` directory using [Pennington.TreeSitter](https://github.com/phil-scott-78/MyLittleContentEngine) (no compilation needed — parsed directly from source).

````markdown
```csharp:symbol
2025/MyProject/MyProject/MyApp.cs > MyClass.MyMethod
```
````

Paths are relative to `ContentRoot` (`../../blog-projects`), so omit the `blog-projects/` prefix.

**Flags** (comma-separated, e.g. `` ```csharp:symbol,bodyonly ``):

| Flag | Effect |
|------|--------|
| `bodyonly` | Strip the declaration line and braces, show only the body |
| `imports` | Prepend the file's top-level `using` statements |
| `signatures` | Collapse member bodies to `{ … }` |

**Variants:**

- `csharp:symbol-diff` — unified diff between two symbol references (one per line, before → after)
- A bare path with no `>` embeds the entire file

**Tabbed widget:** add `tabs=true` as a fence attribute to group adjacent fenced blocks into a tab strip.

### Adding a new code example

1. Create a project in `blog-projects/YYYY/ProjectName/`
2. Add it to `thirty25-blazor.sln`
3. Reference symbols in your post via `:symbol`
4. Link to it in front matter via the `repository` field

### GBNF output artifacts

The `Gbnf` project (`blog-projects/2025/GbnfGeneration/Gbnf/`) writes `.gbnf` and `.json` files to its `output/` directory. These are embedded via `gbnf:symbol` / `json:symbol` with a bare path. To regenerate after changing generator code:

```bash
dotnet run --project blog-projects/2025/GbnfGeneration/Gbnf/Gbnf.csproj
```

Commit the resulting `blog-projects/2025/GbnfGeneration/Gbnf/output/**` files alongside source changes.

## License

MIT License

Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the "Software"), to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
