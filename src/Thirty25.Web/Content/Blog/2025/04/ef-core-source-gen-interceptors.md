---
Title: Automatic Query Tagging in EF Core Using Source Generator Interceptors
description: How to automatically tag your EF Core queries with source location using .NET 8's source generators and interceptors.
date: 2025-04-09
tags:
  - Entity Framework
  - Source Generators
  - Interceptors
repository: https://github.com/phil-scott-78/thirty25-blazor/tree/main/blog-projects/2025/EfCoreTagging
---

It's been a while since I've worked with Entity Framework. While I've been updating my blog engine, I thought I'd
revisit a few of the older posts around tagging and updating them to the latest bits. One thing led to another and next
thing you know I've written a source generator.

## What `TagWith` does in EF Core

I've covered tagging in the past, with my posts 
[Better Tagging of EF Core Queries](/blog/2020/09/tagging-query-with-ef-core) and later on with
[Better Tagging of EF Core Queries with .NET 6](/blog/2021/12/tagging-query-with-ef-core).

But a quick overview to catch you up:

Query tags let you add a comment to the command EF sends to your database provider. Because this tag is a comment
included as part of the command, it will be stored with the statement in your
common DBA tools like SQL Query Store or when viewing them via Extended Events. DBAs monitor these tools looking for
slow running queries. Without a tag, all they'll see is the SQL statement. With a tag, we can at least give them a hint 
at who to blame.

This feature has been around for a while. With EF Core 2.2, Microsoft added the `TagWith` extension method. 
This allows us to write a query such as:

```csharp:xmldocid,bodyonly
M:EfCoreTagging.MyApp.BasicQueryWithTag
```

This generates SQL with a comment:

```sql
-- Getting active blogs from HomeController
SELECT [b].[BlogId], [b].[Name], [b].[Url]
FROM [Blogs] AS [b]
WHERE [b].[IsActive] = 1
```

My previous two articles explored a way to include additional data by using `CallerMemberName` and
`CallerMemberExpression`, but they all had the same limitation - you had to remember to use them. Not only that, but the
nature of relying on `CallerMemberExpression` required them to be placed precisely before a call to `ToListAsync` or you
might miss some info. Plus it wouldn't include the call to `ToListAsync`. Potential for errors abound.

So what if we wanted to take away the opportunity to make this mistake? That's where a new .NET 8 feature called
Interceptors come in to play.

## What are Interceptors?

Interceptors are a newer feature that allow you to, well, intercept method calls at
compile time and redirect them to your own implementation.

Here's how interceptors work:

1. You create a static method that matches the signature of the method you want to intercept (including the `this`
   parameter for extension methods)
2. You apply the `[InterceptsLocation]` attribute to your method, which tells the compiler which specific method calls
   in your code should be intercepted, which contains 
3. At compile time, the C# compiler rewrites those calls to point to your interceptor method instead

For example, with this interceptor:

```csharp
public static class Interceptors 
{
    [InterceptsLocation(1, "p5L8whTIDbQrovY5hnx1/zwCAABNeUFwcC5jcw==")] // internal reference to Program.cs at a location
    public static string ToUpper(this string s) 
    {
        Console.WriteLine("Intercepted ToUpper call!");
        return s.ToUpper();
    }
}
```

Any call to `string.ToUpper()` at that location in Program.cs will be redirected to this method instead.

If you are curious, the arguments to `[InterceptsLocation]` are:

1. a version number of the second parameter's data string. Right now, it's always `1`. 
2. version 1 of said data string, a base64-encoded string consisting of:
   * `16 byte xxHash128` content checksum of the file containing the intercepted call.
   * `int32` in little-endian format for the position (i.e. SyntaxNode.Position) of the call in syntax.
   * `utf-8 string` data containing a display file name, used for error reporting.

Thankfully, we don't need to generate this - there are helper methods that we'll cover in a bit. Given this format,
it's clear this isn't an attribute to be written by hand, but rather programatically, specifically source generation.
What makes this so powerful for our EF Core tagging scenario is:

1. **Zero runtime overhead**: Unlike reflection-based or proxy-based approaches, this happens at compile time
2. **Precise control**: We can target specific method calls rather than all calls to a method
3. **No changes to original code**: The actual source code using EF Core remains untouched
4. **Type safety**: Everything is strongly typed and checked by the compiler

So we can take this knowledge and do a bit of magic. With interceptors, we can detect all EF Core terminal methods (like
`ToList()`, `FirstOrDefault()`, etc.) and automatically inject `TagWith` calls before them without developers having to
change any of their existing LINQ queries.

## Implementing Automatic Query Tagging

The source generator I've built does the following:

1. Detects terminal EF Core method calls (methods that execute a query, like `ToListAsync()`)
2. Generates interceptor methods that:
    - Call `TagWith()` with the original query and source location
    - Call the original terminal method on this tagged query

Let's walk through the key parts of the implementation:

### Finding EF Core Terminal Methods

First we need to initialize our incremental generator.

```csharp:xmldocid
M:EfCoreTagging.SourceGenerator.EfCoreTaggingGenerator.Initialize(Microsoft.CodeAnalysis.IncrementalGeneratorInitializationContext)
```

Here we using a predicate that calls `IsLikelyEfCoreTerminalMethod`

```csharp:xmldocid
M:EfCoreTagging.SourceGenerator.EfCoreTaggingGenerator.IsLikelyEfCoreTerminalMethod(Microsoft.CodeAnalysis.SyntaxNode)
```

This code is used to merely filter out code our source generator is looking at. It's rather simple, with the predicates
in an incremental source generator you don't want to have to use the compilation to keep things fast. So here we are 
merely looking for method invocations that match our method names. We'll filter the rest out in the transform.

### Gathering Location Info

Once we've filtered out unlikely method calls, we move onto generating the data needed to write the interception code. 
Here we do have access to the semantic model, which means we can do more costly operations. We verify that this method
call matches our expected signatures and is an extension method defined in `EntityFrameworkQueryableExtensions`.

Once we know we have code that needs to be intercepted, we call the `GetInterceptableLocation` method. This contains the 
information needed for that base64 encoded string we saw above in the `InterceptsLocation` attribute. Then we gather
up the rest of the information about the call being made so we have the proper data to write our tags, and dump it in
our `MethodCallInfo` DTO.

```csharp:xmldocid
M:EfCoreTagging.SourceGenerator.EfCoreTaggingGenerator.GetMethodCallInfo(Microsoft.CodeAnalysis.GeneratorSyntaxContext)
```

### Generating Interceptors

At this point, we've got a full list of all of our projects calls into the EF terminating extension methods. Now we call
Execute. Most of this method is generating some boilerplate C#. The important part is the `GenerateInterceptorMethod`
which is called for each `MethodCallInfo` from above. 

```csharp
private static void GenerateInterceptorMethod(IndentedTextWriter writer, MethodCallInfo methodCall, int methodIndex)
{
    // Create a unique method name
    var methodName = $"Intercept_{methodSymbol.Name}_{methodIndex}";
        
    // Add the interceptor attribute with location
    writer.WriteLine($"[global::System.Runtime.CompilerServices.InterceptsLocation({methodCall.InterceptableLocation.Version}, \"{methodCall.InterceptableLocation.Data}\")] // {methodCall.DisplayLocation}");
    
    // Method declaration
    writer.Write($"public static {signature.ReturnType} {methodName}{signature.TypeParameters}(");
    writer.WriteLine($"{signature.Parameters})");
        
    // Add the TagWith call
    writer.WriteLine("{");
    writer.Indent++;
    writer.WriteLine("var taggedSource = source.TagWith(");
    writer.WriteLine("\"\"\"");
    writer.WriteLine($"{methodCall.FullMethodCall}");
    writer.WriteLine($"    at {methodCall.CallerInfo}");
    writer.WriteLine("\"\"\");");
    writer.WriteLine();
    
    // Call the original method
    writer.Write("return taggedSource.");
    writer.Write(methodSymbol.Name);
    writer.Write("(");
    writer.Write(string.Join(", ", methodSymbol.Parameters.Skip(1).Select(p => p.Name)));
    writer.WriteLine(");");
        
    writer.Indent--;
    writer.WriteLine("}");
}
```

The key to making this work is the `InterceptsLocation` attribute, which tells the compiler to redirect calls to the
original method to our interceptor instead. The source generator automatically calculates the correct interceptable
location for each method call.

## The Results: Automatic Query Tagging with No Code Changes

Let's see this in action with a simple EF Core query:

```csharp:xmldocid
M:EfCoreTagging.MyApp.RunIt
```

Behind the scenes, our source generator creates an interceptor:

```csharp
static file class EfCoreTaggingInterceptors_MyApp
{
    [global::System.Runtime.CompilerServices.InterceptsLocation(1, "p5L8whTIDbQrovY5hnx1/zwCAABNeUFwcC5jcw==")] // MyApp.cs(16,28)
    public static Task<List<TSource>> Intercept_ToListAsync_0<TSource>(this IQueryable<TSource> source, CancellationToken cancellationToken = default)
    {
        var taggedSource = source.TagWith(
        """
        bloggingContext.Blogs.Where(i => i.Url.StartsWith("https://")).OrderBy(i => i.BlogId).Take(5).ToListAsync()
            at RunIt - B:\thirty25-blazor\blog-projects\2025\EfCoreTagging\MyApp.cs:16
        """);
        
        return taggedSource.ToListAsync(cancellationToken);
    }
}
```

Now when this query runs, the SQL will include a comment showing:

1. The full LINQ expression that was executed
2. The exact location in your code where it was called from

```sql
-- bloggingContext.Blogs.Where(i => i.Url.StartsWith("https://")).OrderBy(i => i.BlogId).Take(5).ToListAsync()
--     at RunIt - B:\thirty25-blazor\blog-projects\2025\EfCoreTagging\MyApp.cs:16

SELECT TOP(5)[b].[BlogId], [b].[Name], [b].[Url]
FROM [Blogs] AS [b]
WHERE [b].[Url] LIKE N'https://%'
ORDER BY [b].[BlogId]
```

This makes it much easier to track down problematic queries during development and debugging, all with n*o changes to our
code* or runtime performance hits.

## Benefits and Limitations

This automated tagging approach provides several benefits:

1. **Zero manual effort**: You don't need to add `TagWith` calls throughout your codebase
2. **Complete coverage**: Every terminal EF Core query gets tagged
3. **Detailed source information**: Each tag includes both the full query and its source location
4. **Compile-time solution**: No runtime performance impact beyond the existing `TagWith` overhead
5. **Minimal Impact to SQL Server Perf**: These tags' content will only change when the source itself changes. 
   The only time there would be a cache-miss on the SQL Server in the query plan cache would be a single time after a
   a change to the caller or file location of the code. 

There are some limitations worth mentioning:

1. **Requires .NET 8**: Method interceptors are only available in .NET 8 and above
2. **Build-time only**: The tagging only happens at build time, so dynamic queries built at runtime won't get the full
   source location
3. **Doesn't work with EF LINQ Query Syntax**. While these do get rewritten by the compiler to use the terminating methods,
   it does so after our source generator runs. Another generator would probably need to be written to handle this case.
   e.g. this would not be included: 
   
   ```csharp
   var result = await (
       from blog in bloggingContext.Blogs
       where blog.Url.StartsWith("https://")
       orderby blog.BlogId
       select blog
   ).Take(5).ToListAsync();
   ```
   
## Conclusion

By combining the power of C# source generators and method interceptors, we've created a zero-effort solution for adding
source context to all our EF Core queries. This makes debugging and profiling much easier, especially in larger
applications with many data access points.

Each query carries its own debugging information that
will show up in profiling tools, making triaging performance issues much simpler.

And special thanks to [Andrew Lock](https://andrewlock.net/). His series on [Creating a Source Generator](https://andrewlock.net/series/creating-a-source-generator/) covering the [interceptors](https://andrewlock.net/creating-a-source-generator-part-11-implementing-an-interceptor-with-a-source-generator/)
inspired this post.