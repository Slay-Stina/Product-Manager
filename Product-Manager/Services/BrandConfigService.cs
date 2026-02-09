using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;
using Product_Manager.Models;

namespace Product_Manager.Services;

public class BrandConfigService
{
    private readonly string _configFilePath;
    private readonly ILogger<BrandConfigService> _logger;
    private readonly JsonSerializerOptions _jsonOptions;

    public BrandConfigService(ILogger<BrandConfigService> logger)
    {
        _logger = logger;
        _configFilePath = Path.Combine("Data", "brand-configs.json");
        
        // Configure JSON options for UTF-8 support
        _jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            Encoder = JavaScriptEncoder.Create(UnicodeRanges.All)
        };
        
        EnsureConfigFileExists();
    }

    private void EnsureConfigFileExists()
    {
        var directory = Path.GetDirectoryName(_configFilePath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        if (!File.Exists(_configFilePath))
        {
            var defaultConfigs = new List<BrandConfig>
            {
                new BrandConfig
                {
                    BrandName = "GANT (Example)",
                    TargetUrl = "https://www.gant.se/",
                    ProductNameSelector = "h1.product-name",
                    ProductPriceSelector = ".price-sales",
                    ProductImageSelector = ".product-image img",
                    ProductDescriptionSelector = ".product-description"
                }
            };
            
            SaveAllConfigs(defaultConfigs);
        }
    }

    public async Task<List<BrandConfig>> GetAllConfigsAsync()
    {
        try
        {
            if (!File.Exists(_configFilePath))
            {
                return new List<BrandConfig>();
            }

            var json = await File.ReadAllTextAsync(_configFilePath, System.Text.Encoding.UTF8);
            return JsonSerializer.Deserialize<List<BrandConfig>>(json, _jsonOptions) ?? new List<BrandConfig>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading brand configurations");
            return new List<BrandConfig>();
        }
    }

    public async Task<BrandConfig?> GetConfigByNameAsync(string brandName)
    {
        var configs = await GetAllConfigsAsync();
        return configs.FirstOrDefault(c => c.BrandName.Equals(brandName, StringComparison.OrdinalIgnoreCase));
    }

    public async Task<bool> SaveConfigAsync(BrandConfig config)
    {
        try
        {
            var configs = await GetAllConfigsAsync();
            
            // Remove existing config with same name
            configs.RemoveAll(c => c.BrandName.Equals(config.BrandName, StringComparison.OrdinalIgnoreCase));
            
            // Add new/updated config
            configs.Add(config);
            
            SaveAllConfigs(configs);
            
            _logger.LogInformation("? Saved configuration for brand: {BrandName}", config.BrandName);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving brand configuration");
            return false;
        }
    }

    public async Task<bool> DeleteConfigAsync(string brandName)
    {
        try
        {
            var configs = await GetAllConfigsAsync();
            configs.RemoveAll(c => c.BrandName.Equals(brandName, StringComparison.OrdinalIgnoreCase));
            
            SaveAllConfigs(configs);
            
            _logger.LogInformation("??? Deleted configuration for brand: {BrandName}", brandName);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting brand configuration");
            return false;
        }
    }

    public void ApplyConfigToCrawlerSettings(BrandConfig brandConfig, CrawlerSettings crawlerSettings)
    {
        crawlerSettings.TargetUrl = brandConfig.TargetUrl;
        crawlerSettings.LoginUrl = brandConfig.LoginUrl;
        crawlerSettings.Username = brandConfig.Username;
        crawlerSettings.Password = brandConfig.Password;
        crawlerSettings.UsernameFieldName = brandConfig.UsernameFieldName;
        crawlerSettings.PasswordFieldName = brandConfig.PasswordFieldName;
        crawlerSettings.MaxPagesToCrawl = brandConfig.MaxPagesToCrawl;
        crawlerSettings.CrawlDelayMilliseconds = brandConfig.CrawlDelayMilliseconds;
        crawlerSettings.ImageDownloadPath = brandConfig.ImageDownloadPath;
        
        brandConfig.LastUsed = DateTime.UtcNow;
    }

    private void SaveAllConfigs(List<BrandConfig> configs)
    {
        var json = JsonSerializer.Serialize(configs, _jsonOptions);
        File.WriteAllText(_configFilePath, json, System.Text.Encoding.UTF8);
    }
}
