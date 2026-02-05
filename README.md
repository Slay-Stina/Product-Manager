# Product Manager

A web-based product management application built with ASP.NET Core Blazor and Entity Framework Core.

## Features

- 🔐 **Secure Authentication** - ASP.NET Core Identity with strong password policies
- 🕷️ **Web Crawler** - Automated product data collection from external sources
- 📊 **Product Database** - Store and manage product information with SQL Server
- 🎨 **Modern UI** - Built with Fluent UI components
- 🔒 **Security First** - Multiple security layers including rate limiting, security headers, and CSRF protection

## Prerequisites

- .NET 9.0 SDK or later
- SQL Server (LocalDB for development, full SQL Server for production)
- Visual Studio 2022 or Visual Studio Code (optional)

## Getting Started

### 1. Clone the Repository

```bash
git clone https://github.com/Slay-Stina/Product-Manager.git
cd Product-Manager
```

### 2. Configure Secrets

For development, use User Secrets to store sensitive configuration:

```bash
cd Product-Manager
dotnet user-secrets set "CrawlerSettings:Username" "your-username"
dotnet user-secrets set "CrawlerSettings:Password" "your-password"
```

See [SECURITY.md](SECURITY.md) for production configuration.

### 3. Run Database Migrations

```bash
cd Product-Manager
dotnet ef database update
```

### 4. Build and Run

```bash
dotnet build
dotnet run
```

The application will be available at `https://localhost:5001` (HTTPS) or `http://localhost:5000` (HTTP).

## Development

### Project Structure

```
Product-Manager/
├── Components/          # Blazor components and pages
│   ├── Account/        # Authentication pages
│   └── ...
├── Data/               # Entity Framework models and DbContext
├── Services/           # Business logic and services
├── Middleware/         # Custom ASP.NET middleware
├── wwwroot/           # Static files
└── Program.cs         # Application entry point
```

### Running Tests

Tests will be added in future iterations. For now, ensure the application builds and runs:

```bash
dotnet build --configuration Release
dotnet run --no-build
```

### Code Quality

Before submitting a pull request, run the local PR check script:

```bash
./check-pr.sh
```

This will:
- ✅ Restore dependencies
- ✅ Build the project
- ✅ Check for vulnerabilities
- ✅ Verify code formatting

## Contributing

### Pull Request Process

1. **Fork the repository** and create your feature branch
   ```bash
   git checkout -b feature/my-new-feature
   ```

2. **Make your changes** following the coding standards

3. **Run local checks** to ensure quality
   ```bash
   ./check-pr.sh
   ```

4. **Commit your changes** with clear messages
   ```bash
   git commit -m "Add some feature"
   ```

5. **Push to your fork** and submit a pull request
   ```bash
   git push origin feature/my-new-feature
   ```

### PR Status Checks

All pull requests must pass automated checks:

- ✅ **Build** - Code must compile successfully
- ✅ **Security Scan** - CodeQL analysis must pass
- ⚠️ **Code Quality** - Formatting should follow .NET conventions

These checks run automatically via GitHub Actions. See [.github/workflows/README.md](.github/workflows/README.md) for details.

### Coding Standards

- Follow standard C# naming conventions
- Use meaningful variable and method names
- Add XML documentation comments for public APIs
- Keep methods focused and concise
- Write secure code - validate inputs, handle errors properly

## Security

Security is a top priority. This application includes:

- 🔒 HTTPS enforcement
- 🛡️ Security headers (CSP, X-Frame-Options, etc.)
- 🚦 Rate limiting
- 🔑 Strong password policies
- 🔐 Account lockout protection

For security guidelines and reporting vulnerabilities, see [SECURITY.md](SECURITY.md).

## Architecture

### Technology Stack

- **Framework**: ASP.NET Core 9.0 with Blazor Server
- **Database**: SQL Server with Entity Framework Core
- **UI Library**: Microsoft Fluent UI for Blazor
- **Crawler**: Abot2 web crawler framework
- **Authentication**: ASP.NET Core Identity

### Security Features

- Security headers middleware
- Rate limiting middleware  
- Request size limits
- CSRF protection via antiforgery tokens
- HSTS for HTTPS enforcement

## License

This project is licensed under the MIT License - see the LICENSE file for details.

## Support

For questions or issues:
- 📝 Open an [Issue](https://github.com/Slay-Stina/Product-Manager/issues)
- 📖 Check the [Documentation](.github/workflows/README.md)
- 🔒 Security issues: See [SECURITY.md](SECURITY.md)

## Acknowledgments

- Built with [ASP.NET Core](https://dotnet.microsoft.com/apps/aspnet)
- UI powered by [Fluent UI Blazor](https://www.fluentui-blazor.net/)
- Web crawling with [Abot](https://github.com/sjdirect/abot)
