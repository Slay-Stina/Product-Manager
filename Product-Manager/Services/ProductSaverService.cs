using Microsoft.EntityFrameworkCore;
using Product_Manager.Data;
using Product_Manager.Models;

namespace Product_Manager.Services;

/// <summary>
/// Service responsible for saving products to the database with batch optimization
/// </summary>
public class ProductSaverService
{
    private readonly ApplicationDbContext _context;
    private readonly ImageDownloaderService _imageDownloader;
    private readonly ILogger<ProductSaverService> _logger;

    private const int BATCH_SIZE = 50;
    private readonly List<Product> _productBatch = new();
    private int _productsSaved = 0;
    private int _productsFailedToBatch = 0;

    public int ProductsSaved => _productsSaved;
    public int ProductsFailedToBatch => _productsFailedToBatch;

    public ProductSaverService(
        ApplicationDbContext context,
        ImageDownloaderService imageDownloader,
        ILogger<ProductSaverService> logger)
    {
        _context = context;
        _imageDownloader = imageDownloader;
        _logger = logger;
    }

    /// <summary>
    /// Add parsed product to batch (will auto-flush when batch size reached)
    /// </summary>
    public async Task AddProductToBatchAsync(ParsedProduct parsedProduct)
    {
        try
        {
            var product = await CreateProductFromParsedDataAsync(parsedProduct);
            _productBatch.Add(product);

            // Auto-flush when batch size reached
            if (_productBatch.Count >= BATCH_SIZE)
            {
                await FlushBatchAsync();
            }
        }
        catch (Exception ex)
        {
            _productsFailedToBatch++;
            _logger.LogError(ex, "❌ Error adding product to batch: {ArticleNumber}", 
                parsedProduct.ArticleNumber);
            throw; // Re-throw to inform caller of failure
        }
    }

    /// <summary>
    /// Create Product entity from parsed data
    /// </summary>
    private async Task<Product> CreateProductFromParsedDataAsync(ParsedProduct parsed)
    {
        var product = new Product
        {
            ArticleNumber = parsed.ArticleNumber!,
            EAN = parsed.EAN,
            ColorId = parsed.ColorId,
            Price = parsed.Price,
            Description = parsed.GetFullDescription(),
            ProductUrl = parsed.ProductUrl,
            CreatedAt = DateTime.UtcNow
        };

        // Download and add images
        if (parsed.ImageUrls.Any())
        {
            var downloadedImages = await _imageDownloader.DownloadImagesAsync(parsed.ImageUrls);

            for (int i = 0; i < downloadedImages.Count; i++)
            {
                var (url, data) = downloadedImages[i];

                var productImage = new ProductImage
                {
                    ImageUrl = url,
                    ImageData = data,
                    Order = i,
                    IsPrimary = i == 0,
                    CreatedAt = DateTime.UtcNow
                };

                product.Images.Add(productImage);
            }
        }

        return product;
    }

    /// <summary>
    /// Flush accumulated products to database in a single batch operation
    /// </summary>
    public async Task FlushBatchAsync()
    {
        if (!_productBatch.Any())
            return;

        try
        {
            _logger.LogInformation("💾 Flushing batch of {Count} products to database...", _productBatch.Count);

            // Get existing products to determine which need updates vs inserts
            var articleNumbers = _productBatch.Select(p => p.ArticleNumber).Distinct().ToList();
            var existingProducts = await _context.Products
                .Include(p => p.Images)
                .Where(p => articleNumbers.Contains(p.ArticleNumber))
                .ToListAsync();

            var existingDict = existingProducts
                .GroupBy(p => (p.ArticleNumber, p.ColorId))
                .ToDictionary(g => g.Key, g => g.First());

            int updatedCount = 0;
            int insertedCount = 0;

            foreach (var product in _productBatch)
            {
                var key = (product.ArticleNumber, product.ColorId);

                if (existingDict.TryGetValue(key, out var existing))
                {
                    // Update existing product
                    UpdateExistingProduct(existing, product);
                    updatedCount++;
                }
                else
                {
                    // Insert new product
                    _context.Products.Add(product);
                    insertedCount++;
                }
            }

            await _context.SaveChangesAsync();
            _productsSaved += _productBatch.Count;

            _logger.LogInformation("✅ Batch saved: {Inserted} new, {Updated} updated", 
                insertedCount, updatedCount);

            _productBatch.Clear();
        }
        catch (Exception ex)
        {
            // Clear the batch to avoid retrying the same products indefinitely
            var batchCount = _productBatch.Count;
            var sampleArticles = string.Join(", ", _productBatch.Take(5).Select(p => p.ArticleNumber));
            _productBatch.Clear();

            _logger.LogError(
                ex,
                "❌ Error flushing product batch. Cleared batch of {Count} products. Sample article numbers: {Articles}",
                batchCount,
                sampleArticles
            );
            throw;
        }
    }

    /// <summary>
    /// Update existing product with new data
    /// </summary>
    private void UpdateExistingProduct(Product existing, Product updated)
    {
        existing.EAN = updated.EAN;
        existing.Price = updated.Price;
        existing.Description = updated.Description;
        existing.ProductUrl = updated.ProductUrl;
        existing.UpdatedAt = DateTime.UtcNow;

        // Replace images - create new instances to avoid EF tracking issues
        _context.ProductImages.RemoveRange(existing.Images);
        existing.Images.Clear();
        
        foreach (var image in updated.Images)
        {
            existing.Images.Add(new ProductImage
            {
                ProductId = existing.Id,
                ImageUrl = image.ImageUrl,
                ImageData = image.ImageData,
                Order = image.Order,
                IsPrimary = image.IsPrimary
            });
        }
    }

    /// <summary>
    /// Save a single product immediately (not batched) - for legacy support
    /// </summary>
    public async Task SaveProductAsync(
        string articleNumber, 
        string? colorId, 
        string? description, 
        string? imageUrl, 
        string? productUrl = null)
    {
        try
        {
            var existingProduct = await _context.Products
                .Include(p => p.Images)
                .FirstOrDefaultAsync(p => p.ArticleNumber == articleNumber && p.ColorId == colorId);

            if (existingProduct != null)
            {
                existingProduct.Description = description;
                existingProduct.ProductUrl = productUrl;
                existingProduct.UpdatedAt = DateTime.UtcNow;

                if (!string.IsNullOrEmpty(imageUrl))
                {
                    var imageData = await _imageDownloader.DownloadImageAsync(imageUrl);

                    // Update or create primary image
                    var primaryImage = existingProduct.Images.FirstOrDefault(i => i.IsPrimary);
                    if (primaryImage != null)
                    {
                        primaryImage.ImageUrl = imageUrl;
                        primaryImage.ImageData = imageData;
                    }
                    else
                    {
                        existingProduct.Images.Add(new ProductImage
                        {
                            ImageUrl = imageUrl,
                            ImageData = imageData,
                            Order = 0,
                            IsPrimary = true,
                            CreatedAt = DateTime.UtcNow
                        });
                    }
                }

                _logger.LogInformation("♻️  Updated existing product");
            }
            else
            {
                var product = new Product
                {
                    ArticleNumber = articleNumber,
                    ColorId = colorId,
                    Description = description,
                    ProductUrl = productUrl,
                    CreatedAt = DateTime.UtcNow
                };

                if (!string.IsNullOrEmpty(imageUrl))
                {
                    var imageData = await _imageDownloader.DownloadImageAsync(imageUrl);
                    product.Images.Add(new ProductImage
                    {
                        ImageUrl = imageUrl,
                        ImageData = imageData,
                        Order = 0,
                        IsPrimary = true,
                        CreatedAt = DateTime.UtcNow
                    });
                }

                _context.Products.Add(product);
                _logger.LogInformation("➕ Created new product");
            }

            await _context.SaveChangesAsync();
            _productsSaved++;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving product {ArticleNumber}", articleNumber);
        }
    }

    /// <summary>
    /// Reset statistics
    /// </summary>
    public void ResetStatistics()
    {
        _productsSaved = 0;
        _productsFailedToBatch = 0;
    }
}
