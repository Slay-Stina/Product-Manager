using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Product_Manager.Data;

public class Product
{
    [Key]
    public int Id { get; set; }

    [Required]
    [MaxLength(100)]
    public string ArticleNumber { get; set; } = string.Empty;

    [MaxLength(100)]
    public string? EAN { get; set; }

    [MaxLength(50)]
    public string? ColorId { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal? Price { get; set; }

    [MaxLength(2000)]
    public string? Description { get; set; }

    [MaxLength(500)]
    public string? ProductUrl { get; set; }

    [Obsolete("Use Images collection instead")]
    public string? ImageUrl { get; set; }

    [Obsolete("Use Images collection instead")]
    public byte[]? ImageData { get; set; }

    public ICollection<ProductImage> Images { get; set; } = new List<ProductImage>();

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }
}
