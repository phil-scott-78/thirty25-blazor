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

### `:symbol` Syntax - Embedding Code from Projects

You can embed live source code from the `blog-projects/` directory. `Pennington.TreeSitter`
reads the `.cs` source directly via tree-sitter (no compilation needed):

````markdown
```csharp:symbol
RelativePath/To/File.cs > ClassName.MemberName
```
````

**How it works:**
1. Create a working C# project in `blog-projects/YYYY/ProjectName/`
2. Add the project to `thirty25-blazor.sln` (for IDE support / compilation)
3. Reference a member with `path > Type.Member`, where `path` is **relative to `ContentRoot`**
   (set to `../../blog-projects` in `Program.cs`) — so drop the `blog-projects/` prefix.
4. The member path is a dotted chain of *declaration names* — no namespace
   (e.g. `LlamaGrammar.TestPerson`, `MyApp.BasicQueryWithTag`). A bare path with no `>`
   embeds the whole file.

**Flags** (comma-separated after `:symbol`, order-independent):
- `,bodyonly` - Show only the member body (strips the declaration line and braces)
- `,imports` - Prepend the file's top-of-file `using` statements
- `,signatures` - Collapse member bodies to `{ … }` (shows a type's shape only)

**Other forms:**
- `csharp:symbol-diff` - Unified diff between two references (one per line, before → after); accepts `,bodyonly`
- `tabs=true` - Space-separated attribute that groups adjacent fences into a tabbed widget (composes with `:symbol`)

**Example:**
````markdown
```csharp:symbol,bodyonly
2025/EfCoreTagging/EfCoreTagging/MyApp.cs > MyApp.BasicQueryWithTag
```
````

## blog-projects Directory

The `blog-projects/` directory contains working code examples that are:
- Included in the solution for IDE support
- Referenced in blog posts via the `:symbol` syntax
- Linked in post front matter via the `repository` field

When creating a new blog post with code examples:
1. Create a project in `blog-projects/YYYY/ProjectName/`
2. Add it to `thirty25-blazor.sln`
3. Reference it in your blog post markdown using the `:symbol` syntax
4. Link to it in front matter with the `repository` field

## Regenerating Gbnf Output Artifacts

The `Gbnf` project (`blog-projects/2025/GbnfGeneration/Gbnf/`) emits `.gbnf` and `.json` artifacts into its `output/` directory. These are embedded whole-file from the markdown posts with `gbnf:symbol` / `json:symbol` and a bare path (no `>`), e.g. `2025/GbnfGeneration/Gbnf/output/MyApp.GetSimpleGbnf.gbnf` (relative to `ContentRoot`).

To regenerate after changing any of the example generator code:

```bash
dotnet run --project blog-projects/2025/GbnfGeneration/Gbnf/Gbnf.csproj
```

The resulting `blog-projects/2025/GbnfGeneration/Gbnf/output/**` files are checked in — commit them along with any source changes.

## Key Configuration

Main configuration is in `src/Thirty25.Web/Program.cs`:
- `AddPenningtonTreeSitter(opts => opts.ContentRoot = "../../blog-projects")` - Root that `:symbol` file paths resolve against
- `ContentPath = "Content/blog/"` - Where markdown files are located
- `BlogPath = "/blog"` - Base URL path for blog posts
