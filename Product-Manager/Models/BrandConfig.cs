namespace Product_Manager.Models;

public class BrandConfig
{
    public string BrandName { get; set; } = string.Empty;
    public string TargetUrl { get; set; } = string.Empty;
    public string LoginUrl { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string UsernameFieldName { get; set; } = "username";
    public string PasswordFieldName { get; set; } = "password";
    public int MaxPagesToCrawl { get; set; } = 100;
    public int CrawlDelayMilliseconds { get; set; } = 1000;
    public string ImageDownloadPath { get; set; } = "wwwroot/images/products";
    
    // Brand-specific selectors
    public string ProductContainerSelector { get; set; } = string.Empty;
    public string ProductNameSelector { get; set; } = string.Empty;
    public string ProductPriceSelector { get; set; } = string.Empty;
    public string ProductImageSelector { get; set; } = string.Empty;
    public string ProductDescriptionSelector { get; set; } = string.Empty;
    public string ProductSkuSelector { get; set; } = string.Empty;
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastUsed { get; set; }
}
