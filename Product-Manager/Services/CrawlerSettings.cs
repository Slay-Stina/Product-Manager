namespace Product_Manager.Services;

public class CrawlerSettings
{
    public string TargetUrl { get; set; } = string.Empty;
    public string LoginUrl { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string UsernameFieldName { get; set; } = "username";
    public string PasswordFieldName { get; set; } = "password";
    public int MaxPagesToCrawl { get; set; } = 100;
    public int CrawlDelayMilliseconds { get; set; } = 1000;
    public string ImageDownloadPath { get; set; } = "wwwroot/images/products";
}
