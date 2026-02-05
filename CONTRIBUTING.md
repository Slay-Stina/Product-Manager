# Contributing to Product Manager

Thank you for your interest in contributing to the Product Manager project! This guide will help you get started.

## Table of Contents

- [Code of Conduct](#code-of-conduct)
- [Getting Started](#getting-started)
- [Development Workflow](#development-workflow)
- [Pull Request Process](#pull-request-process)
- [Coding Standards](#coding-standards)
- [Testing](#testing)
- [Security](#security)

## Code of Conduct

- Be respectful and inclusive
- Focus on constructive feedback
- Help create a welcoming environment for all contributors

## Getting Started

### Prerequisites

- .NET 9.0 SDK or later
- Git
- A GitHub account
- SQL Server or LocalDB (for development)

### Setting Up Your Development Environment

1. **Fork the repository** on GitHub

2. **Clone your fork**:
   ```bash
   git clone https://github.com/YOUR-USERNAME/Product-Manager.git
   cd Product-Manager
   ```

3. **Add upstream remote**:
   ```bash
   git remote add upstream https://github.com/Slay-Stina/Product-Manager.git
   ```

4. **Configure secrets** for the crawler (see [SECURITY.md](SECURITY.md)):
   ```bash
   cd Product-Manager
   dotnet user-secrets set "CrawlerSettings:Username" "your-username"
   dotnet user-secrets set "CrawlerSettings:Password" "your-password"
   ```

5. **Run the application**:
   ```bash
   dotnet build
   dotnet run
   ```

## Development Workflow

### Creating a Feature Branch

Always create a new branch for your work:

```bash
git checkout -b feature/your-feature-name
```

Branch naming conventions:
- `feature/` - New features
- `fix/` - Bug fixes
- `docs/` - Documentation updates
- `refactor/` - Code refactoring
- `test/` - Adding or updating tests

### Making Changes

1. **Write clean, maintainable code**
   - Follow C# coding conventions
   - Add comments for complex logic
   - Keep methods focused and concise

2. **Test your changes locally**
   ```bash
   ./check-pr.sh
   ```

3. **Commit your changes**
   ```bash
   git add .
   git commit -m "Brief description of changes"
   ```

   Commit message format:
   ```
   Type: Brief description (50 chars or less)
   
   More detailed explanation if needed (72 chars per line)
   
   - List specific changes
   - Reference issues if applicable (#123)
   ```

### Keeping Your Fork Updated

Regularly sync with the upstream repository:

```bash
git fetch upstream
git checkout main
git merge upstream/main
git push origin main
```

## Pull Request Process

### Before Submitting

1. **Run local checks**:
   ```bash
   ./check-pr.sh
   ```

2. **Ensure your branch is up to date**:
   ```bash
   git fetch upstream
   git rebase upstream/main
   ```

3. **Review your changes**:
   ```bash
   git diff upstream/main
   ```

### Submitting a Pull Request

1. **Push your branch**:
   ```bash
   git push origin feature/your-feature-name
   ```

2. **Create a PR** on GitHub with:
   - Clear title describing the change
   - Description of what and why
   - Reference to any related issues
   - Screenshots for UI changes

3. **PR Template**:
   ```markdown
   ## Description
   Brief description of changes
   
   ## Type of Change
   - [ ] Bug fix
   - [ ] New feature
   - [ ] Breaking change
   - [ ] Documentation update
   
   ## Testing
   - [ ] Tested locally
   - [ ] All checks pass
   
   ## Related Issues
   Fixes #123
   ```

### PR Review Process

1. **Automated Checks**: Your PR will trigger GitHub Actions:
   - ✅ Build must pass
   - ✅ Security scan must pass
   - ⚠️ Code quality checks (warnings only)

2. **Code Review**: A maintainer will review your PR:
   - Address feedback promptly
   - Push updates to the same branch
   - Request re-review when ready

3. **Approval**: Once approved and checks pass, a maintainer will merge your PR

## Coding Standards

### C# Style Guide

Follow standard C# conventions:

```csharp
// ✅ Good
public class ProductService
{
    private readonly ApplicationDbContext _context;
    
    public async Task<Product?> GetProductAsync(string articleNumber)
    {
        return await _context.Products
            .FirstOrDefaultAsync(p => p.ArticleNumber == articleNumber);
    }
}

// ❌ Avoid
public class productservice
{
    private ApplicationDbContext context;
    
    public Product? getProduct(string article)
    {
        return context.Products
            .FirstOrDefault(p=>p.ArticleNumber==article);
    }
}
```

### Naming Conventions

- **Classes**: PascalCase - `ProductService`, `ApplicationDbContext`
- **Methods**: PascalCase - `GetProductAsync`, `SaveChanges`
- **Properties**: PascalCase - `ArticleNumber`, `CreatedAt`
- **Fields**: camelCase with underscore - `_context`, `_logger`
- **Parameters**: camelCase - `articleNumber`, `productId`
- **Constants**: PascalCase - `MaxRetries`, `DefaultTimeout`

### File Organization

```
Product-Manager/
├── Components/         # UI components (.razor files)
├── Data/              # Data models and DbContext
├── Services/          # Business logic
├── Middleware/        # Custom middleware
└── Program.cs         # Entry point
```

### Comments and Documentation

```csharp
/// <summary>
/// Retrieves a product by its article number.
/// </summary>
/// <param name="articleNumber">The unique article number</param>
/// <returns>The product if found, null otherwise</returns>
public async Task<Product?> GetProductAsync(string articleNumber)
{
    // Validate input
    if (string.IsNullOrWhiteSpace(articleNumber))
        throw new ArgumentException("Article number cannot be empty", nameof(articleNumber));
    
    // Query database
    return await _context.Products
        .FirstOrDefaultAsync(p => p.ArticleNumber == articleNumber);
}
```

## Testing

### Running Tests

When tests are added to the project:

```bash
dotnet test
```

### Writing Tests

- Write unit tests for business logic
- Write integration tests for database operations
- Aim for meaningful test coverage

Example:
```csharp
[Fact]
public async Task GetProductAsync_ValidArticleNumber_ReturnsProduct()
{
    // Arrange
    var service = new ProductService(_context);
    
    // Act
    var result = await service.GetProductAsync("ART123");
    
    // Assert
    Assert.NotNull(result);
    Assert.Equal("ART123", result.ArticleNumber);
}
```

## Security

### Security Best Practices

- **Never commit secrets** - Use User Secrets or environment variables
- **Validate all inputs** - Prevent injection attacks
- **Handle errors properly** - Don't expose sensitive information
- **Follow least privilege** - Grant minimal necessary permissions
- **Keep dependencies updated** - Check for vulnerabilities regularly

### Reporting Security Issues

**Do not open a public issue for security vulnerabilities.**

Instead, email security concerns to the maintainers or follow the process in [SECURITY.md](SECURITY.md).

## Questions?

- 📝 Open an issue for bugs or feature requests
- 💬 Start a discussion for questions
- 📖 Read the [README](README.md) for general information
- 🔒 See [SECURITY.md](SECURITY.md) for security guidelines

## License

By contributing, you agree that your contributions will be licensed under the same license as the project.

---

Thank you for contributing to Product Manager! 🎉
