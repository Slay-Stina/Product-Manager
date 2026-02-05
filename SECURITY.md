# Security Guidelines

## Sensitive Configuration

This application uses ASP.NET Core User Secrets for sensitive configuration in development and environment variables in production.

### Development Setup

To configure crawler credentials in development:

```bash
cd Product-Manager
dotnet user-secrets set "CrawlerSettings:Username" "your-actual-username"
dotnet user-secrets set "CrawlerSettings:Password" "your-actual-password"
```

### Production Setup

In production, use environment variables or Azure Key Vault:

```bash
# Environment variables
export CrawlerSettings__Username="your-actual-username"
export CrawlerSettings__Password="your-actual-password"
```

Or in Azure App Service, configure Application Settings:
- `CrawlerSettings:Username` = your-actual-username
- `CrawlerSettings:Password` = your-actual-password

### Database Connection

The default connection string uses LocalDB for development. For production:

1. Set the connection string via environment variable:
   ```bash
   export ConnectionStrings__DefaultConnection="Server=your-server;Database=your-db;..."
   ```

2. Or use Azure Key Vault for secure storage.

## Security Features

This application includes:

1. **HTTPS Enforcement** - All traffic is redirected to HTTPS
2. **HSTS** - HTTP Strict Transport Security enabled
3. **Antiforgery Protection** - CSRF tokens on all forms
4. **Security Headers** - Content Security Policy, X-Frame-Options, etc.
5. **Rate Limiting** - Protection against DoS attacks (100 requests/min per IP)
6. **ASP.NET Core Identity** - Secure authentication and authorization
7. **Request Size Limits** - Protection against large payload attacks (10MB max)

### Known Dependencies

The Abot web crawler library (v2.0.70) has transitive dependencies on old System.Net.Http and System.Security.Cryptography packages that show vulnerability warnings. These warnings can be safely ignored in .NET 9.0, as the framework uses its own built-in implementations of these types instead of the old package versions.

## Reporting Security Issues

If you discover a security vulnerability, please email security@example.com instead of using the issue tracker.

## Password Policy

- Minimum 8 characters (configurable)
- Requires uppercase and lowercase letters
- Requires at least one digit
- Requires at least one non-alphanumeric character
- Email confirmation required
- Supports two-factor authentication
- Password reset via email
- Account lockout after 5 failed attempts (5 minute lockout)

## Best Practices

1. Never commit credentials to version control
2. Use User Secrets in development
3. Use environment variables or Key Vault in production
4. Regularly update dependencies
5. Enable Application Insights for monitoring
6. Review security logs regularly
