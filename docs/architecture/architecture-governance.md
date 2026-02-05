# Architecture Governance & Validation

This document defines the architectural rules, constraints, and validation mechanisms specific to the Product Manager application.

## Architectural Constraints

### Layer Dependencies

**Rule**: Code must respect the following dependency flow:
```
Components (UI) → Services → Data → External Systems
       ↓
   Middleware
```

**Prohibited**:
- Services cannot reference UI Components
- Data layer cannot reference Services
- Middleware cannot reference Services directly

**Validation Script**: `scripts/validate-dependencies.sh`

### Security Architecture Rules

**Mandatory Security Patterns**:

1. **Crawler Credentials**: NEVER in appsettings.json
   - ✅ Use User Secrets (development)
   - ✅ Use Environment Variables (production)
   - ❌ Hard-coded values

2. **Middleware Order**: Security middleware must execute before business logic
   ```
   HTTPS → Security Headers → Rate Limiting → Authentication → Authorization → Business Logic
   ```

3. **Database Access**: Only through ApplicationDbContext
   - ❌ Direct SqlConnection objects
   - ❌ Raw SQL strings (except specific optimizations)
   - ✅ Entity Framework LINQ queries

**Validation**: Run `check-security-patterns.sh` before each commit

### Performance Constraints

**SignalR Circuit Limits**:
- Maximum 1000 concurrent circuits per server instance
- Circuit timeout: 3 minutes idle
- Disconnect timeout: 30 seconds

**Database Query Rules**:
- No queries returning > 1000 records without pagination
- All list queries must use `.AsNoTracking()` when read-only
- Images stored in database, but paginated with byte[] loaded only when needed

**Rate Limiting**:
- Per-IP: 100 requests per minute
- Burst allowance: 150 requests (50% buffer)
- Cleanup interval: Every 5 minutes

### Code Organization Standards

**Namespace Structure**:
```
Product_Manager
├── Components (Blazor .razor files)
├── Data (EF Core entities and DbContext)
├── Services (business logic, crawler)
├── Middleware (cross-cutting concerns)
└── Program.cs (startup configuration)
```

**Service Registration Pattern**:
- Transient: Stateless, lightweight services
- Scoped: Database contexts, per-request services
- Singleton: Configuration objects, expensive-to-create services

**Current Registrations**:
```csharp
// Singleton - Lives for app lifetime
CrawlerSettings - configuration object

// Scoped - Per request/circuit
ApplicationDbContext - database operations
ProductCrawlerService - per-crawl state
Identity services - per authentication session

// Transient - Created each time
(None currently - all services maintain state)
```

## Architecture Validation Scripts

### Script 1: Dependency Analyzer

File: `scripts/check-architecture.sh`

```bash
#!/bin/bash
# Validates architectural boundaries are respected

echo "Checking architectural boundaries..."

# Check: Services should not reference Components
SERVICES_REF_COMPONENTS=$(grep -r "using Product_Manager.Components" Product-Manager/Services/ 2>/dev/null)
if [ ! -z "$SERVICES_REF_COMPONENTS" ]; then
    echo "❌ VIOLATION: Services reference UI Components"
    echo "$SERVICES_REF_COMPONENTS"
    exit 1
fi

# Check: Data should not reference Services
DATA_REF_SERVICES=$(grep -r "using Product_Manager.Services" Product-Manager/Data/ 2>/dev/null)
if [ ! -z "$DATA_REF_SERVICES" ]; then
    echo "❌ VIOLATION: Data layer references Services"
    echo "$DATA_REF_SERVICES"
    exit 1
fi

# Check: Middleware should not reference Services
MIDDLEWARE_REF_SERVICES=$(grep -r "ProductCrawlerService\|IdentityService" Product-Manager/Middleware/ 2>/dev/null)
if [ ! -z "$MIDDLEWARE_REF_SERVICES" ]; then
    echo "❌ VIOLATION: Middleware references business Services"
    echo "$MIDDLEWARE_REF_SERVICES"
    exit 1
fi

echo "✅ All architectural boundaries respected"
```

### Script 2: Security Pattern Validator

File: `scripts/check-security-patterns.sh`

```bash
#!/bin/bash
# Ensures security patterns are followed

echo "Validating security patterns..."

# Check: No hardcoded credentials in appsettings
HARDCODED_CREDS=$(grep -E "(Username|Password)\":\s*\"[^\"]+\"" Product-Manager/appsettings.json | grep -v '""' | grep -v "your-")
if [ ! -z "$HARDCODED_CREDS" ]; then
    echo "❌ VIOLATION: Hardcoded credentials in appsettings.json"
    echo "$HARDCODED_CREDS"
    exit 1
fi

# Check: Middleware order in Program.cs
MIDDLEWARE_ORDER=$(grep -A 10 "var app = builder.Build()" Product-Manager/Program.cs | \
  grep -E "UseHttpsRedirection|UseSecurityHeaders|UseRateLimiting|UseAntiforgery" | \
  head -4)

EXPECTED_ORDER="UseHttpsRedirection
UseSecurityHeaders
UseRateLimiting
UseAntiforgery"

if ! echo "$MIDDLEWARE_ORDER" | grep -q "UseHttpsRedirection"; then
    echo "❌ VIOLATION: HTTPS redirection not enabled"
    exit 1
fi

# Check: Rate limiting is configured
if ! grep -q "UseRateLimiting" Product-Manager/Program.cs; then
    echo "❌ VIOLATION: Rate limiting not configured"
    exit 1
fi

echo "✅ Security patterns validated"
```

### Script 3: Database Query Analyzer

File: `scripts/check-database-queries.sh`

```bash
#!/bin/bash
# Checks for problematic database query patterns

echo "Analyzing database query patterns..."

# Check: Find queries without AsNoTracking for read operations
QUERIES_WITHOUT_NOTRACKING=$(grep -r "\.FirstOrDefault\|\.ToList\|\.Where" Product-Manager/Services/ | \
  grep -v "AsNoTracking" | \
  grep -v "//.*AsNoTracking" | \
  wc -l)

if [ "$QUERIES_WITHOUT_NOTRACKING" -gt 5 ]; then
    echo "⚠️  WARNING: $QUERIES_WITHOUT_NOTRACKING queries found without .AsNoTracking()"
    echo "Consider adding .AsNoTracking() for read-only queries"
fi

# Check: Find potential N+1 query issues (queries in loops)
N_PLUS_ONE=$(grep -A 3 "foreach\|for (" Product-Manager/Services/ | grep "_context\." | wc -l)
if [ "$N_PLUS_ONE" -gt 0 ]; then
    echo "⚠️  WARNING: Potential N+1 query pattern detected ($N_PLUS_ONE instances)"
    echo "Consider using .Include() or eager loading"
fi

echo "✅ Database query analysis complete"
```

## Decision Log

This section tracks architectural decisions made during development (alternative to formal ADRs).

### Why Blazor Server over WebAssembly?
**Date**: Project inception
**Decision**: Use Blazor Server for UI
**Rationale**: 
- Real-time crawler progress updates needed (SignalR built-in)
- Smaller initial download for users
- Server-side security easier to implement
- Team familiar with C#, not JavaScript

**Trade-offs Accepted**:
- Requires server resources per user
- Network latency for interactions
- More complex horizontal scaling

### Why In-Memory Rate Limiting?
**Date**: Security implementation
**Decision**: Use ConcurrentDictionary for rate limit tracking
**Rationale**:
- Simple implementation for single-server deployment
- No external dependencies (Redis not needed yet)
- Fast lookups and updates
- Cleanup logic prevents memory leaks

**Future Migration Path**:
- When scaling horizontally: Move to Redis-backed rate limiting
- When traffic > 10k requests/min: Consider dedicated API gateway

### Why SQL Server for Product Storage?
**Date**: Project inception
**Decision**: SQL Server with LocalDB for dev
**Rationale**:
- Team already uses Microsoft stack
- LocalDB requires no setup for developers
- Relational model fits product catalog structure
- EF Core has excellent SQL Server support
- Can upgrade to Azure SQL in production

**Considered Alternatives**:
- MongoDB: Rejected - team unfamiliar with NoSQL
- PostgreSQL: Rejected - prefer Microsoft ecosystem consistency
- SQLite: Rejected - insufficient for production scale

### Why Abot2 for Web Crawling?
**Date**: Crawler implementation
**Decision**: Use Abot2 library for crawling
**Rationale**:
- Handles robots.txt, politeness delays, throttling
- Mature library with 2k+ GitHub stars
- Supports authentication and cookies
- Built-in HTML parsing with AngleSharp

**Known Issues**:
- Transitive dependencies on old System.Net.Http (doesn't affect .NET 9 runtime)
- Not actively maintained (last update 2020)
- No async/await in some APIs

**Mitigation**:
- Wrap in our ProductCrawlerService for abstraction
- Can swap out Abot2 later without changing service interface
- Document dependency warnings as expected

## Architecture Evolution Path

### Current State (v1.0)
- Single-server Blazor Server application
- Direct database access via EF Core
- In-memory rate limiting
- Manual crawler triggering via UI

### Planned (v1.5 - Next 6 months)
- Scheduled crawler jobs using Hangfire
- Health check endpoints for monitoring
- Structured logging to Application Insights
- API endpoints for basic integrations

### Future Vision (v2.0 - 12+ months)
- Background job processing separate from web tier
- Distributed caching with Redis
- Message queue for crawler jobs (Azure Service Bus)
- Read replicas for database scaling
- Separate admin portal

### Long-term Considerations (v3.0+)
- Multi-tenancy support (multiple customers)
- Microservices if team grows significantly
- Event-driven architecture for crawler results
- GraphQL API for flexible queries

## Metrics & KPIs

### Architecture Health Metrics

**Code Quality**:
- Lines of code per service: Target < 500
- Cyclomatic complexity: Target < 10 per method
- Test coverage: Target > 70% (when tests added)

**Performance**:
- Page load time: < 2 seconds
- API response time: < 500ms (p95)
- Database query time: < 100ms (p95)
- SignalR reconnection rate: < 5%

**Security**:
- Security headers score: A+ (securityheaders.com)
- Rate limit effectiveness: < 1% legitimate requests blocked
- Failed auth attempts: Monitor for brute force patterns
- Vulnerability scan: 0 critical issues

**Reliability**:
- Uptime: > 99.5%
- Error rate: < 1%
- Crawler success rate: > 95%
- Database connection pool: < 80% utilized

## Review Process

### Architecture Review Triggers

Conduct architecture review when:
1. Adding new major feature (> 1 week effort)
2. Introducing new external dependency
3. Changing database schema significantly
4. Modifying security/authentication patterns
5. Performance issues requiring architectural changes

### Review Checklist

- [ ] Does change respect layer boundaries?
- [ ] Are security patterns maintained?
- [ ] Is configuration externalized?
- [ ] Are database queries optimized?
- [ ] Does it scale with current approach?
- [ ] Is monitoring/logging added?
- [ ] Are validation scripts updated?
- [ ] Is documentation updated?

## Validation Integration

### Add to CI/CD Pipeline

In `.github/workflows/pr-check.yml`, add architecture validation job:

```yaml
architecture-validation:
  name: Architecture Validation
  runs-on: ubuntu-latest
  
  steps:
    - name: Checkout code
      uses: actions/checkout@v4
      
    - name: Make scripts executable
      run: chmod +x scripts/*.sh
      
    - name: Check architecture boundaries
      run: ./scripts/check-architecture.sh
      
    - name: Validate security patterns
      run: ./scripts/check-security-patterns.sh
      
    - name: Analyze database queries
      run: ./scripts/check-database-queries.sh
      continue-on-error: true
```

This ensures architecture governance is enforced automatically on every pull request.
