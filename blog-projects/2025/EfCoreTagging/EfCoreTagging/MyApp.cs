using System.Data.Common;
using System.Diagnostics.CodeAnalysis;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace EfCoreTagging;

[SuppressMessage("ReSharper", "UnusedVariable")]
internal static class MyApp
{
    public static async Task BasicQueryWithTag()
    {
        await using var connection = await GetConnection();
        await using var bloggingContext = await GetContext(connection);

        var activeBlogs = await bloggingContext.Blogs
            .Where(b => b.IsActive)
            .TagWith("Getting active blogs from HomeController")
            .ToDictionaryAsync(blog => blog.BlogId);
    }

    public static async Task RunIt()
    {
        await using var connection = await GetConnection();
        await using var bloggingContext = await GetContext(connection);

        var result = await bloggingContext.Blogs
            .Where(i => i.Url.StartsWith("https://"))
            .OrderBy(i => i.BlogId)
            .Take(5)
            .ToListAsync();
    }

    private static async Task<BloggingContext> GetContext(DbConnection connection)
    {
        var options = new DbContextOptionsBuilder<BloggingContext>()
            .UseSqlite(connection)
            .LogTo(Console.WriteLine, LogLevel.Information)
            .Options;

        var bloggingContext = new BloggingContext(options);
        await bloggingContext.Database.EnsureCreatedAsync();
        return bloggingContext;
    }

    private static async Task<SqliteConnection> GetConnection()
    {
        SqliteConnection? connection = null;
        try
        {
            connection = new SqliteConnection("Filename=:memory:");
            connection.Open();
            return connection;
        }
        catch
        {
            if (connection != null) await connection.DisposeAsync();
            throw;
        }
    }
    
}