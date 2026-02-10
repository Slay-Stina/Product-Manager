# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added
- Brand configuration system for multi-site crawling support
- GANT Sweden brand configuration with site-specific selectors
- Automatic brand detection based on target URL domain
- Enhanced image extraction supporting multiple lazy-loading attributes (`data-src`, `data-original`, `srcset`)
- Proper Blazor component disposal handling in Products page
- Rate limiting middleware for API protection
- Security headers middleware

### Changed
- **[BREAKING]** Increased `Product.Description` max length from 500 to 2000 characters
- Updated `ProductCrawlerService` to use brand-specific selectors instead of hardcoded ones
- Replaced `StreamRendering` with proper `InteractiveServer` mode in Products component
- Changed `StateHasChanged()` calls in Products component to use `await InvokeAsync(StateHasChanged)` for thread safety
- Updated `ExtractImageUrl()` to check multiple image source attributes

### Fixed
- **Database truncation error** when saving products with long descriptions (500 ? 2000 char limit)
- **DbContext configuration override** issue where Fluent API was overriding model annotations
- **Blazor component disposal error** (`The renderer does not have a component with ID X`)
- **Images not detected** on modern e-commerce sites using lazy loading
- **GANT crawler** not capturing products due to incorrect selectors
- Thread safety issues in Blazor Server components with async state updates

### Database Migrations
- `20260209144048_IncreaseDescriptionLength` - Obsolete/no-op migration (empty Up/Down methods, kept for history)
- `20260209145251_UpdateDescriptionLengthInDbContext` - Increased `Product.Description` to nvarchar(2000) and updated DbContext Fluent API to match model

## [0.1.0] - Initial Release

### Added
- Basic web crawler using Abot2 library
- Product data model with support for images
- SQL Server database with Entity Framework Core
- Blazor Server UI for viewing products
- Manual crawler configuration via appsettings.json
- Unicode support for international product names
- Image downloading and storage capabilities

---

## Migration Notes

### Upgrading to Latest Version

If you have an existing database, run migrations:

```bash
dotnet ef database update
```

This will:
- Increase the `Description` column size to accommodate longer product descriptions
- Preserve all existing product data

### Breaking Changes

- **Description field size change**: Any code relying on 500-character limit should be updated
- **Blazor rendering mode**: Products page no longer uses StreamRendering, only InteractiveServer

---

## Recent Fix Details

### February 9, 2026 - Product Description & Image Fixes

#### Issue 1: Database Truncation
Products with combined name + price + description exceeded 500 characters.

**Solution:**
- Updated `Product.cs` model `[MaxLength(2000)]`
- Updated `ApplicationDbContext.cs` Fluent API `HasMaxLength(2000)`
- Applied two migrations to update database schema

#### Issue 2: Images Not Detected
Modern sites use lazy-loading with `data-src` instead of standard `src` attribute.

**Solution:**
Enhanced `ExtractImageUrl()` to check:
1. `src` (standard)
2. `data-src` (lazy-loading)
3. `data-original` (alternative)
4. `data-lazy-src` (variant)
5. `srcset` (responsive images)

#### Issue 3: Blazor Component Errors
Combining `StreamRendering` + `InteractiveServer` caused race conditions.

**Solution:**
- Removed `[StreamRendering]` attribute
- Added `IDisposable` implementation
- Changed to `await InvokeAsync(StateHasChanged)` for thread safety
- Added disposal guards to prevent updates after component disposal

### Brand Configuration System

The crawler now supports multiple brands through JSON configuration files.

**Key Features:**
- Automatic brand detection from target URL
- Per-brand CSS selectors for product extraction
- GANT Sweden configuration included as example
- Easy to add new brands without code changes

**Usage:**
1. Go to Brand Configs page
2. Load a brand configuration
3. Start crawler - it auto-detects and applies the right selectors

---

## Development Notes

### Important: EF Core Configuration Precedence

When working with Entity Framework Core, remember:

**Fluent API (OnModelCreating) > Data Annotations**

Always keep both in sync:
```csharp
// Product.cs
[MaxLength(2000)]
public string? Description { get; set; }

// ApplicationDbContext.cs
entity.Property(p => p.Description)
    .HasMaxLength(2000);  // Must match!
```

### Blazor Component Best Practices

For interactive components:
- ? Implement `IDisposable`
- ? Use `await InvokeAsync(StateHasChanged)`
- ? Check disposal before state updates
- ? Don't combine `StreamRendering` + `InteractiveServer`
- ? Don't call `StateHasChanged()` directly in async methods

---

## Contributors

- Slay-Stina ([@Slay-Stina](https://github.com/Slay-Stina))

## License

This project is licensed under the MIT License - see the LICENSE file for details.
