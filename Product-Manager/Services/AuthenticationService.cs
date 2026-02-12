using System.Net;

namespace Product_Manager.Services;

/// <summary>
/// Service responsible for handling web authentication
/// </summary>
public class AuthenticationService
{
    private readonly CrawlerSettings _settings;
    private readonly ILogger<AuthenticationService> _logger;
    private CookieContainer _cookieContainer;

    public CookieContainer CookieContainer => _cookieContainer;

    public AuthenticationService(
        CrawlerSettings settings,
        ILogger<AuthenticationService> logger)
    {
        _settings = settings;
        _logger = logger;
        _cookieContainer = new CookieContainer();
    }

    /// <summary>
    /// Authenticate to the target website using configured credentials
    /// </summary>
    public async Task<bool> AuthenticateAsync()
    {
        try
        {
            _logger.LogInformation("🔐 Attempting to authenticate at {LoginUrl}", _settings.LoginUrl);
            
            // Check if authentication is needed
            if (IsAuthenticationNotConfigured())
            {
                _logger.LogWarning("⚠️ Login URL is not configured or uses example.com. Skipping authentication.");
                _logger.LogInformation("ℹ️  If the site doesn't require login, this is OK. Otherwise, update CrawlerSettings in appsettings.json");
                return true; // Allow crawling without authentication for public sites
            }

            if (AreCredentialsMissing())
            {
                _logger.LogWarning("⚠️ Username or password is empty. Skipping authentication.");
                _logger.LogInformation("ℹ️  If the site doesn't require login, this is OK. Otherwise, configure credentials.");
                return true; // Allow crawling without authentication
            }

            return await PerformLoginAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Error during authentication");
            return false;
        }
    }

    /// <summary>
    /// Check if authentication is not configured
    /// </summary>
    private bool IsAuthenticationNotConfigured()
    {
        return string.IsNullOrWhiteSpace(_settings.LoginUrl) || 
               _settings.LoginUrl.Contains("example.com", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Check if credentials are missing
    /// </summary>
    private bool AreCredentialsMissing()
    {
        return string.IsNullOrWhiteSpace(_settings.Username) || 
               string.IsNullOrWhiteSpace(_settings.Password);
    }

    /// <summary>
    /// Perform the actual login POST request
    /// </summary>
    private async Task<bool> PerformLoginAsync()
    {
        using var loginData = new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>(_settings.UsernameFieldName, _settings.Username),
            new KeyValuePair<string, string>(_settings.PasswordFieldName, _settings.Password)
        });

        var handler = new HttpClientHandler
        {
            CookieContainer = _cookieContainer,
            UseCookies = true,
            AllowAutoRedirect = true
        };

        using var client = new HttpClient(handler);
        var response = await client.PostAsync(_settings.LoginUrl, loginData);

        if (response.IsSuccessStatusCode)
        {
            _logger.LogInformation("✅ Authentication successful");
            return true;
        }

        _logger.LogWarning("⚠️ Authentication failed with status code: {StatusCode}", response.StatusCode);
        
        if (_logger.IsEnabled(LogLevel.Debug))
        {
            var responseBody = await response.Content.ReadAsStringAsync();
            var preview = responseBody.Length > 500 ? responseBody.Substring(0, 500) : responseBody;
            _logger.LogDebug("Response body: {ResponseBody}", preview);
        }
        
        return false;
    }

    /// <summary>
    /// Reset the cookie container (for re-authentication)
    /// </summary>
    public void ResetCookies()
    {
        // Clear cookies by expiring them to maintain references
        var cookieCollection = _cookieContainer.GetAllCookies();
        foreach (Cookie cookie in cookieCollection)
        {
            cookie.Expired = true;
            cookie.Expires = DateTime.UtcNow.AddDays(-1); // Set past date for proper expiration
        }
        _logger.LogDebug("🔄 Cookie container reset");
    }
}
