# Architecture Overview

## System Purpose

The Product Manager is a web-based application for managing product information with automated web crawling capabilities. It provides secure authentication, product data storage, and automated product information collection from external sources.

## High-Level Architecture

```mermaid
graph TB
    subgraph "Client Layer"
        Browser[Web Browser]
    end
    
    subgraph "Application Layer - ASP.NET Core 9.0"
        BlazorServer[Blazor Server]
        SignalR[SignalR Hub]
        Middleware[Middleware Pipeline]
        
        subgraph "Components"
            UI[UI Components]
            Account[Account Pages]
        end
        
        subgraph "Services"
            CrawlerSvc[Product Crawler Service]
            IdentitySvc[Identity Services]
        end
        
        subgraph "Security"
            Auth[Authentication/Authorization]
            Headers[Security Headers]
            RateLimit[Rate Limiting]
        end
    end
    
    subgraph "Data Layer"
        EFCore[Entity Framework Core]
        DbContext[Application DbContext]
    end
    
    subgraph "External Systems"
        DB[(SQL Server Database)]
        ExternalSite[External Product Sites]
    end
    
    Browser <-->|HTTPS/SignalR| BlazorServer
    BlazorServer <--> SignalR
    Browser --> Middleware
    Middleware --> Auth
    Middleware --> Headers
    Middleware --> RateLimit
    BlazorServer --> UI
    BlazorServer --> Account
    UI --> CrawlerSvc
    UI --> IdentitySvc
    CrawlerSvc --> EFCore
    IdentitySvc --> EFCore
    EFCore --> DbContext
    DbContext --> DB
    CrawlerSvc -.->|HTTP| ExternalSite
    
    style BlazorServer fill:#90EE90
    style DB fill:#87CEEB
    style Auth fill:#FFB6C1
    style ExternalSite fill:#FFE4B5
```

## Layered Architecture

### Presentation Layer
- **Blazor Components** - Interactive UI components using Fluent UI
- **Razor Pages** - Account management pages (login, register, etc.)
- **SignalR** - Real-time communication between server and browser

### Application Layer
- **Services** - Business logic (Product Crawler, Identity management)
- **Middleware** - Cross-cutting concerns (security, rate limiting)
- **Controllers** - API endpoints (if needed in future)

### Data Layer
- **Entity Framework Core** - ORM for data access
- **DbContext** - Database context and configuration
- **Migrations** - Database schema versioning

### Infrastructure Layer
- **SQL Server** - Persistent data storage
- **Configuration** - appsettings.json, User Secrets, Environment Variables
- **Logging** - Built-in ASP.NET Core logging

## Key Patterns

### Dependency Injection
All services are registered in `Program.cs` and injected where needed:
```csharp
builder.Services.AddScoped<ProductCrawlerService>();
builder.Services.AddSingleton<CrawlerSettings>();
```

### Repository Pattern (via EF Core)
DbContext acts as a Unit of Work and repository:
```csharp
public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
{
    public DbSet<Product> Products { get; set; }
}
```

### Middleware Pipeline
Security and cross-cutting concerns implemented as middleware:
- HTTPS Redirection
- Security Headers
- Rate Limiting
- Antiforgery Protection

### Options Pattern
Configuration bound to strongly-typed classes:
```csharp
var crawlerSettings = new CrawlerSettings();
builder.Configuration.GetSection("CrawlerSettings").Bind(crawlerSettings);
```

## Data Flow

### Product Crawling Flow
```mermaid
sequenceDiagram
    participant User
    participant UI
    participant CrawlerService
    participant HttpClient
    participant ExternalSite
    participant DbContext
    participant Database

    User->>UI: Start Crawl
    UI->>CrawlerService: StartCrawlingAsync()
    CrawlerService->>HttpClient: Authenticate
    HttpClient->>ExternalSite: POST /login
    ExternalSite-->>HttpClient: Auth Cookies
    CrawlerService->>HttpClient: Crawl Pages
    HttpClient->>ExternalSite: GET /products
    ExternalSite-->>HttpClient: HTML Content
    CrawlerService->>CrawlerService: Parse Products
    CrawlerService->>DbContext: SaveProduct()
    DbContext->>Database: INSERT/UPDATE
    Database-->>DbContext: Success
    DbContext-->>CrawlerService: Saved
    CrawlerService-->>UI: Complete
    UI-->>User: Results
```

### Authentication Flow
```mermaid
sequenceDiagram
    participant User
    participant Browser
    participant Middleware
    participant Identity
    participant Database

    User->>Browser: Enter Credentials
    Browser->>Middleware: POST /login
    Middleware->>Identity: SignInAsync()
    Identity->>Database: Verify Credentials
    Database-->>Identity: User Valid
    Identity->>Identity: Create Auth Cookie
    Identity-->>Middleware: Signed In
    Middleware-->>Browser: Set Cookie + Redirect
    Browser-->>User: Dashboard
```

## Security Architecture

### Defense in Depth Layers

1. **Transport Security** - HTTPS with HSTS
2. **Authentication** - ASP.NET Core Identity with strong passwords
3. **Authorization** - Role-based access control
4. **Input Validation** - Antiforgery tokens, model validation
5. **Output Encoding** - Blazor auto-escaping
6. **Security Headers** - CSP, X-Frame-Options, etc.
7. **Rate Limiting** - DoS protection (100 req/min per IP)
8. **Request Limits** - Max body size, header size limits

### Security Middleware Pipeline Order
```
1. UseHttpsRedirection()
2. UseSecurityHeaders()
3. UseRateLimiting()
4. UseAuthentication()
5. UseAuthorization()
6. UseAntiforgery()
```

## Database Schema

### Core Entities

```mermaid
erDiagram
    ApplicationUser ||--o{ Product : creates
    
    ApplicationUser {
        string Id PK
        string UserName
        string Email
        string PasswordHash
        bool EmailConfirmed
        int AccessFailedCount
    }
    
    Product {
        int Id PK
        string ArticleNumber UK
        string ColorId
        string Description
        string ImageUrl
        byte[] ImageData
        DateTime CreatedAt
        DateTime UpdatedAt
    }
```

## Scalability Considerations

### Current State (Single Instance)
- Blazor Server maintains SignalR connections
- In-memory rate limiting (per-instance)
- LocalDB for development, SQL Server for production

### Future Scaling Options
1. **Horizontal Scaling**
   - Move to Blazor WebAssembly for stateless client
   - Use distributed cache (Redis) for rate limiting
   - Connection pooling for database

2. **Vertical Scaling**
   - Increase server resources
   - Optimize database queries
   - Add caching layer

3. **Database Scaling**
   - Read replicas for queries
   - Partitioning by customer/date
   - Azure SQL elastic pools

## Monitoring & Observability

### Logging
- Built-in ASP.NET Core logging
- Log levels: Information, Warning, Error
- Structured logging ready for Application Insights

### Health Checks (Future)
- Database connectivity
- External service availability
- Memory/CPU metrics

## Technology Decisions

See [Architecture Decision Records (ADRs)](../adr/) for detailed rationale on:
- ADR-001: Blazor Server vs Blazor WebAssembly
- ADR-002: Entity Framework Core for data access
- ADR-003: ASP.NET Core Identity for authentication
- ADR-004: SQL Server as database
- ADR-005: Middleware-based security architecture
