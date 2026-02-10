using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Product_Manager.Data
{
    public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : IdentityDbContext<ApplicationUser>(options)
    {
        public DbSet<Product> Products { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Configure Product entity for proper Unicode support
            modelBuilder.Entity<Product>(entity =>
            {
                entity.Property(p => p.ArticleNumber)
                    .IsUnicode(true)
                    .HasMaxLength(100);

                entity.Property(p => p.ColorId)
                    .IsUnicode(true)
                    .HasMaxLength(50);

                entity.Property(p => p.Description)
                    .IsUnicode(true)
                    .HasMaxLength(2000);

                entity.Property(p => p.ImageUrl)
                    .IsUnicode(true);

                // Create index for faster lookups
                entity.HasIndex(p => new { p.ArticleNumber, p.ColorId })
                    .IsUnique();
            });
        }
    }
}
