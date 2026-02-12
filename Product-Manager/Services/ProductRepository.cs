using Microsoft.EntityFrameworkCore;
using Product_Manager.Data;

namespace Product_Manager.Services;

/// <summary>
/// Repository for Product data access operations
/// Separates data access concerns from business logic
/// </summary>
public class ProductRepository
{
    private readonly ApplicationDbContext _context;

    public ProductRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Get all products with their images
    /// </summary>
    public async Task<List<Product>> GetAllProductsAsync()
    {
        return await _context.Products
            .Include(p => p.Images)
            .ToListAsync();
    }

    /// <summary>
    /// Get a specific product by article number
    /// </summary>
    public async Task<Product?> GetProductByArticleNumberAsync(string articleNumber)
    {
        return await _context.Products
            .Include(p => p.Images)
            .FirstOrDefaultAsync(p => p.ArticleNumber == articleNumber);
    }

    /// <summary>
    /// Get products by article numbers (batch lookup)
    /// </summary>
    public async Task<List<Product>> GetProductsByArticleNumbersAsync(List<string> articleNumbers)
    {
        return await _context.Products
            .Include(p => p.Images)
            .Where(p => articleNumbers.Contains(p.ArticleNumber))
            .ToListAsync();
    }

    /// <summary>
    /// Search products by description or article number
    /// </summary>
    public async Task<List<Product>> SearchProductsAsync(string searchTerm, int maxResults = 100)
    {
        var lowerSearchTerm = searchTerm.ToLower();

        return await _context.Products
            .Include(p => p.Images)
            .Where(p => 
                p.ArticleNumber.ToLower().Contains(lowerSearchTerm) ||
                (p.Description != null && p.Description.ToLower().Contains(lowerSearchTerm)) ||
                (p.EAN != null && p.EAN.ToLower().Contains(lowerSearchTerm)))
            .Take(maxResults)
            .ToListAsync();
    }

    /// <summary>
    /// Get products count
    /// </summary>
    public async Task<int> GetProductsCountAsync()
    {
        return await _context.Products.CountAsync();
    }

    /// <summary>
    /// Check if a product exists
    /// </summary>
    public async Task<bool> ProductExistsAsync(string articleNumber, string? colorId = null)
    {
        return await _context.Products
            .AnyAsync(p => p.ArticleNumber == articleNumber && p.ColorId == colorId);
    }

    /// <summary>
    /// Delete a product
    /// </summary>
    public async Task<bool> DeleteProductAsync(int productId)
    {
        var product = await _context.Products
            .Include(p => p.Images)
            .FirstOrDefaultAsync(p => p.Id == productId);

        if (product == null)
            return false;

        _context.Products.Remove(product);
        await _context.SaveChangesAsync();
        return true;
    }

    /// <summary>
    /// Delete all products
    /// </summary>
    public async Task<int> DeleteAllProductsAsync()
    {
        var count = await _context.Products.CountAsync();
        
        // Use ExecuteDeleteAsync for efficient bulk deletion
        await _context.Products.ExecuteDeleteAsync();
        
        return count;
    }
}
