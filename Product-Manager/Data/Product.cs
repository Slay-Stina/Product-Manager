using System.ComponentModel.DataAnnotations;

namespace Product_Manager.Data;

public class Product
{
    [Key]
    public int Id { get; set; }

    [Required]
    [MaxLength(100)]
    public string ArticleNumber { get; set; } = string.Empty;

    [MaxLength(50)]
    public string? ColorId { get; set; }

    [MaxLength(500)]
    public string? Description { get; set; }

    public string? ImageUrl { get; set; }

    public byte[]? ImageData { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }
}
