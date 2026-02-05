# C4 Model Diagrams

This document presents the system architecture using the C4 model (Context, Container, Component, Code).

## Level 1: System Context

Shows how the Product Manager system fits into the wider environment.

```mermaid
graph TB
    User[Product Manager<br/>User]
    Admin[System<br/>Administrator]
    
    subgraph "Product Manager System"
        System[Product Manager<br/>Web Application]
    end
    
    ExtSite[External Product<br/>Websites]
    Email[Email<br/>Service]
    
    User -->|Views and manages<br/>products| System
    Admin -->|Configures and<br/>monitors| System
    System -->|Crawls product<br/>data from| ExtSite
    System -.->|Sends notifications<br/>via| Email
    
    style System fill:#90EE90
    style ExtSite fill:#FFE4B5
    style Email fill:#D3D3D3
```

### System Context Description

**Product Manager System**: Web-based application for managing product information with automated web crawling.

**Users**:
- **Product Managers**: View, search, and manage product catalog
- **System Administrators**: Configure crawler settings, manage users

**External Systems**:
- **External Product Websites**: Source of product data for crawling
- **Email Service** (Future): For notifications and account management

## Level 2: Container Diagram

Shows the high-level technical building blocks.

```mermaid
graph TB
    User[User<br/>Web Browser]
    
    subgraph "Product Manager System"
        WebApp[Blazor Server<br/>Web Application<br/>.NET 9.0]
        Database[(SQL Server<br/>Database)]
    end
    
    ExtSite[External<br/>Websites]
    
    User -->|HTTPS<br/>SignalR| WebApp
    WebApp -->|ADO.NET<br/>EF Core| Database
    WebApp -->|HTTP/HTTPS| ExtSite
    
    style WebApp fill:#90EE90
    style Database fill:#87CEEB
    style ExtSite fill:#FFE4B5
```

### Container Descriptions

**Blazor Server Web Application**
- Technology: ASP.NET Core 9.0, Blazor Server, C#
- Purpose: Hosts UI and business logic
- Communication: HTTPS with SignalR for real-time updates

**SQL Server Database**
- Technology: SQL Server (LocalDB for dev, full SQL Server for prod)
- Purpose: Stores user accounts, products, and application state
- Access: Entity Framework Core ORM

## Level 3: Component Diagram

Shows the major components within the web application container.

```mermaid
graph TB
    subgraph "Web Browser"
        Browser[Browser]
    end
    
    subgraph "Blazor Server Application"
        subgraph "Presentation Components"
            UIComponents[Blazor UI<br/>Components]
            AccountPages[Account<br/>Pages]
        end
        
        subgraph "Middleware Pipeline"
            SecurityHeaders[Security Headers<br/>Middleware]
            RateLimiting[Rate Limiting<br/>Middleware]
            Auth[Authentication<br/>Middleware]
        end
        
        subgraph "Application Services"
            CrawlerService[Product Crawler<br/>Service]
            IdentityService[Identity<br/>Services]
        end
        
        subgraph "Data Access"
            DbContext[Application<br/>DbContext]
            Entities[Entity<br/>Models]
        end
        
        subgraph "Configuration"
            Settings[Configuration<br/>Settings]
        end
    end
    
    subgraph "External"
        DB[(Database)]
        ExtSite[External Sites]
    end
    
    Browser <-->|SignalR| UIComponents
    Browser --> SecurityHeaders
    SecurityHeaders --> RateLimiting
    RateLimiting --> Auth
    Auth --> UIComponents
    Auth --> AccountPages
    
    UIComponents --> CrawlerService
    UIComponents --> IdentityService
    AccountPages --> IdentityService
    
    CrawlerService --> DbContext
    CrawlerService --> Settings
    IdentityService --> DbContext
    
    DbContext --> Entities
    DbContext --> DB
    CrawlerService -.-> ExtSite
    
    style UIComponents fill:#E6F3FF
    style CrawlerService fill:#FFE6E6
    style DbContext fill:#E6FFE6
    style SecurityHeaders fill:#FFB6C1
```

### Component Descriptions

#### Presentation Layer

**Blazor UI Components**
- Responsibilities: Render UI, handle user interactions
- Technology: Razor components, Fluent UI library
- Examples: Product list, product details, dashboard

**Account Pages**
- Responsibilities: User authentication flows
- Technology: Razor Pages, ASP.NET Core Identity
- Examples: Login, Register, Password Reset

#### Middleware Layer

**Security Headers Middleware**
- Responsibilities: Add security headers (CSP, X-Frame-Options, etc.)
- Pattern: ASP.NET Core Middleware
- Execution: Early in pipeline

**Rate Limiting Middleware**
- Responsibilities: Prevent DoS attacks, limit requests per IP
- Pattern: Custom middleware with in-memory tracking
- Limits: 100 requests/minute per IP

**Authentication Middleware**
- Responsibilities: Validate user identity
- Technology: ASP.NET Core Identity
- Features: Cookie-based auth, lockout, 2FA support

#### Application Services

**Product Crawler Service**
- Responsibilities: Crawl external sites, extract product data
- Dependencies: Abot2 crawler, HttpClient, DbContext
- Pattern: Scoped service with dependency injection

**Identity Services**
- Responsibilities: User management, authentication
- Technology: ASP.NET Core Identity
- Features: User CRUD, password management, roles

#### Data Access Layer

**Application DbContext**
- Responsibilities: Database operations, change tracking
- Technology: Entity Framework Core
- Pattern: Unit of Work + Repository

**Entity Models**
- Responsibilities: Domain models
- Examples: ApplicationUser, Product
- Mapping: Code-First with migrations

## Level 4: Code Diagram (Selected Components)

### Product Crawler Service Class Diagram

```mermaid
classDiagram
    class ProductCrawlerService {
        -ApplicationDbContext _context
        -CrawlerSettings _settings
        -ILogger _logger
        -HttpClient _httpClient
        -CookieContainer _cookieContainer
        +ProductCrawlerService(context, settings, logger, httpClientFactory)
        +Task~bool~ AuthenticateAsync()
        +Task StartCrawlingAsync()
        +Task~List~Product~~ GetAllProductsAsync()
        -void ProcessPage(sender, args)
        -void ParseAndSaveProducts(document, url)
        -void SaveProduct(articleNumber, colorId, description, imageUrl)
        -Task~byte[]~ DownloadImage(imageUrl)
    }
    
    class CrawlerSettings {
        +string TargetUrl
        +string LoginUrl
        +string Username
        +string Password
        +string UsernameFieldName
        +string PasswordFieldName
        +int MaxPagesToCrawl
        +int CrawlDelayMilliseconds
        +string ImageDownloadPath
    }
    
    class ApplicationDbContext {
        +DbSet~Product~ Products
        +DbSet~ApplicationUser~ Users
    }
    
    class Product {
        +int Id
        +string ArticleNumber
        +string ColorId
        +string Description
        +string ImageUrl
        +byte[] ImageData
        +DateTime CreatedAt
        +DateTime UpdatedAt
    }
    
    ProductCrawlerService --> CrawlerSettings : uses
    ProductCrawlerService --> ApplicationDbContext : uses
    ProductCrawlerService ..> Product : creates/updates
```

### Security Middleware Class Diagram

```mermaid
classDiagram
    class SecurityHeadersMiddleware {
        -RequestDelegate _next
        +SecurityHeadersMiddleware(next)
        +Task InvokeAsync(context)
    }
    
    class RateLimitingMiddleware {
        -RequestDelegate _next
        -ILogger _logger
        -static ConcurrentDictionary _requestCounts
        -int _maxRequestsPerMinute
        +RateLimitingMiddleware(next, logger)
        +Task InvokeAsync(context)
        -Task CleanupOldEntriesAsync(now)
    }
    
    class RequestCounter {
        +object Lock
        +List~DateTime~ Timestamps
    }
    
    RateLimitingMiddleware --> RequestCounter : manages
```

## Component Interactions

### Crawling Process Sequence

```mermaid
sequenceDiagram
    participant UI as UI Component
    participant CS as CrawlerService
    participant HC as HttpClient
    participant ES as External Site
    participant DC as DbContext
    participant DB as Database

    UI->>CS: StartCrawlingAsync()
    CS->>CS: AuthenticateAsync()
    CS->>HC: PostAsync(loginUrl)
    HC->>ES: POST /login
    ES-->>HC: Set-Cookie
    HC-->>CS: Authenticated
    
    CS->>CS: Configure Crawler
    loop For Each Page
        CS->>HC: GET page
        HC->>ES: GET /products
        ES-->>HC: HTML
        HC-->>CS: Page Content
        CS->>CS: ParseAndSaveProducts()
        CS->>DC: Add/Update Product
        DC->>DB: INSERT/UPDATE
        DB-->>DC: Success
    end
    
    CS-->>UI: Crawl Complete
```

### Request Processing Pipeline

```mermaid
sequenceDiagram
    participant Browser
    participant SH as Security Headers
    participant RL as Rate Limiter
    participant Auth as Authentication
    participant UI as UI Component
    participant Svc as Service
    participant DB as Database

    Browser->>SH: HTTPS Request
    SH->>SH: Add Security Headers
    SH->>RL: Continue
    RL->>RL: Check Rate Limit
    alt Rate Limit Exceeded
        RL-->>Browser: 429 Too Many Requests
    else Within Limit
        RL->>Auth: Continue
        Auth->>Auth: Validate Cookie
        alt Not Authenticated
            Auth-->>Browser: Redirect to Login
        else Authenticated
            Auth->>UI: Render Component
            UI->>Svc: Business Logic
            Svc->>DB: Query/Update
            DB-->>Svc: Data
            Svc-->>UI: Result
            UI-->>Browser: Rendered HTML + SignalR
        end
    end
```

## Deployment View

Shows how components are deployed to infrastructure.

```mermaid
graph TB
    subgraph "Azure Cloud / On-Premises"
        subgraph "Web Server"
            IIS[IIS / Kestrel]
            App[Product Manager<br/>Application]
        end
        
        subgraph "Database Server"
            SQL[(SQL Server)]
        end
        
        subgraph "External"
            Sites[External Product<br/>Websites]
        end
    end
    
    Users[Users] -->|HTTPS| IIS
    IIS --> App
    App -->|TLS| SQL
    App -.->|HTTPS| Sites
    
    style App fill:#90EE90
    style SQL fill:#87CEEB
    style Sites fill:#FFE4B5
```

## Technology Mapping

| Layer | Technology | Version |
|-------|-----------|---------|
| Frontend | Blazor Server | .NET 9.0 |
| UI Library | Microsoft Fluent UI | 4.13.2 |
| Backend Framework | ASP.NET Core | 9.0 |
| ORM | Entity Framework Core | 9.0.9 |
| Database | SQL Server | 2019+ |
| Authentication | ASP.NET Core Identity | 9.0.9 |
| Web Crawler | Abot2 | 2.0.70 |
| Logging | ILogger | Built-in |
| DI Container | Microsoft.Extensions.DI | Built-in |

## Cross-Cutting Concerns

### Security
- Implemented via middleware pipeline
- Multiple defense layers
- Documented in SECURITY.md

### Logging
- Structured logging via ILogger
- Injected into all services
- Configurable log levels

### Configuration
- appsettings.json for defaults
- User Secrets for development
- Environment variables for production
- Options pattern for strongly-typed config

### Error Handling
- Global exception handler in production
- Developer exception page in development
- Validation errors at model level

## Evolution and Future State

### Current Architecture (v1.0)
- Monolithic Blazor Server application
- Single database instance
- In-memory rate limiting
- Manual crawler triggers

### Planned Improvements (v2.0)
- Scheduled crawler jobs (Hangfire/Quartz)
- Distributed caching (Redis)
- Message queue for async processing
- Health checks and metrics
- API endpoints for integration

### Future Consideration (v3.0)
- Microservices architecture
- Event-driven with message bus
- CQRS pattern for scalability
- Separate read/write databases
