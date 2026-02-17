using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Product_Manager.Data
{
    public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : IdentityDbContext<ApplicationUser>(options)
    {
        public DbSet<Product> Products { get; set; }
        public DbSet<ProductImage> ProductImages { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Configure Product entity for proper Unicode support
            modelBuilder.Entity<Product>(entity =>
            {
                entity.Property(p => p.ArticleNumber)
                    .IsUnicode(true)
                    .HasMaxLength(100);

                entity.Property(p => p.Name)
                    .IsRequired()
                    .IsUnicode(true)
                    .HasMaxLength(200);

                entity.Property(p => p.EAN)
                    .IsUnicode(true)
                    .HasMaxLength(100);

                entity.Property(p => p.ColorId)
                    .IsUnicode(true)
                    .HasMaxLength(50);

                entity.Property(p => p.Description)
                    .IsUnicode(true)
                    .HasMaxLength(2000);

                entity.Property(p => p.ProductUrl)
                    .IsUnicode(true)
                    .HasMaxLength(500);

                entity.Property(p => p.Price)
                    .HasColumnType("decimal(18,2)");

                // Create index for faster lookups
                entity.HasIndex(p => new { p.ArticleNumber, p.ColorId })
                    .IsUnique();

                // Configure relationship with ProductImages
                entity.HasMany(p => p.Images)
                    .WithOne(i => i.Product)
                    .HasForeignKey(i => i.ProductId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // Configure ProductImage entity
            modelBuilder.Entity<ProductImage>(entity =>
            {
                entity.Property(i => i.ImageUrl)
                    .IsUnicode(true);

                entity.HasIndex(i => new { i.ProductId, i.Order });
            });
        }
    }
}
