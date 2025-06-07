using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace EfCoreTagging
{
    public class BloggingContext(DbContextOptions options) : DbContext(options)
    {
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
        }

        public DbSet<Blog> Blogs { get; set; }
    }
    
    public class Blog
    {
        public required int BlogId { get; init; }
        [MaxLength(2048)]
        public required string Url { get; init; }
        public required bool IsActive { get; init; }
    }
}