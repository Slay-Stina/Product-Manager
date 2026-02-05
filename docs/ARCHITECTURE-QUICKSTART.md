# Architecture as Code - Quick Reference

## 📚 What's Been Implemented

The Product Manager application now has comprehensive "Architecture as Code" with:

### Documentation (`docs/architecture/`)

| File | Purpose | Size |
|------|---------|------|
| [README.md](architecture/README.md) | Overview and principles | Guide |
| [architecture-overview.md](architecture/architecture-overview.md) | System architecture, layers, security | 7.3 KB |
| [c4-diagrams.md](architecture/c4-diagrams.md) | C4 model with Mermaid diagrams | 11.8 KB |
| [deployment-architecture.md](architecture/deployment-architecture.md) | Azure/On-prem deployment guides | 10.2 KB |
| [architecture-governance.md](architecture/governance.md) | Rules, constraints, decision log | 11.2 KB |

### Decision Records (`docs/adr/`)

- **ADR-001**: Why Blazor Server over WebAssembly
- **ADR-002**: Why Entity Framework Core for data access
- **Template**: For documenting future decisions

### Validation Scripts (`scripts/`)

| Script | Checks | Blocks Merge? |
|--------|--------|---------------|
| `check-architecture.sh` | Layer dependencies | ✅ Yes |
| `check-security-patterns.sh` | Security rules | ✅ Yes |
| `check-database-queries.sh` | Query patterns | ⚠️ No (warnings) |

## 🚀 Quick Start

### View Architecture

```bash
# Open main architecture doc
cat docs/architecture/architecture-overview.md

# View C4 diagrams (best in GitHub)
cat docs/architecture/c4-diagrams.md

# Check deployment options
cat docs/architecture/deployment-architecture.md
```

### Validate Code

```bash
# Run all architecture checks
bash scripts/check-architecture.sh
bash scripts/check-security-patterns.sh
bash scripts/check-database-queries.sh

# Or use existing check-pr.sh which includes these
./check-pr.sh
```

### CI/CD Integration

Architecture validation runs automatically on every PR:

```yaml
architecture-validation:
  name: Architecture Validation
  - Check architecture boundaries
  - Validate security patterns  
  - Analyze database queries
```

## 📖 Architecture Overview

### System Layers

```
┌─────────────────────────────────┐
│   Components (Blazor UI)        │ ← User Interface
├─────────────────────────────────┤
│   Services (Business Logic)     │ ← Application Logic
├─────────────────────────────────┤
│   Data (EF Core + Entities)     │ ← Data Access
├─────────────────────────────────┤
│   Database (SQL Server)         │ ← Persistence
└─────────────────────────────────┘
     ↕
┌─────────────────────────────────┐
│   Middleware (Security, etc.)   │ ← Cross-Cutting
└─────────────────────────────────┘
```

### Key Patterns

- **Dependency Injection**: Services registered and injected
- **Middleware Pipeline**: Security-first request processing
- **Repository via DbContext**: Unit of Work pattern
- **Options Pattern**: Strongly-typed configuration

### Technology Stack

- **UI**: Blazor Server + Fluent UI
- **Backend**: ASP.NET Core 9.0
- **Data**: Entity Framework Core + SQL Server
- **Security**: Identity, middleware-based defense
- **Crawler**: Abot2 library

## 🔒 Architectural Constraints

### Must Follow

1. **Layer Dependencies**: Components → Services → Data (one direction)
2. **No Hardcoded Credentials**: Use User Secrets or env vars
3. **Security Middleware Order**: HTTPS → Headers → Rate Limit → Auth
4. **DbContext Only in Services**: No direct database access elsewhere

### Should Follow

1. **Use AsNoTracking()**: On read-only queries
2. **Include() for Related Data**: Avoid N+1 queries
3. **Paginate Large Results**: Don't load 1000+ records
4. **Project Binary Data**: Don't eager-load images

## 📊 Visual Architecture

See `docs/architecture/c4-diagrams.md` for:

- **System Context**: How we fit in the ecosystem
- **Container Diagram**: High-level technical blocks
- **Component Diagram**: Internal structure
- **Class Diagrams**: Key component details
- **Sequence Diagrams**: Crawler and auth flows

All diagrams use Mermaid and render beautifully in GitHub!

## 🎯 Decision Process

### When to Create an ADR

Create an Architecture Decision Record when:

- Adding significant new technology/library
- Changing security approach
- Modifying data access patterns
- Architectural refactoring
- Performance optimization requiring trade-offs

### ADR Template

Copy `docs/adr/adr-template.md` and fill in:

1. **Status**: Proposed/Accepted/Deprecated
2. **Context**: Why the decision is needed
3. **Decision**: What was chosen
4. **Alternatives**: What was considered
5. **Consequences**: Trade-offs accepted

## 📈 Evolution Path

### Current (v1.0)

- Monolithic Blazor Server
- Single database
- In-memory rate limiting
- Manual crawler triggers

### Planned (v1.5)

- Scheduled crawler jobs (Hangfire)
- Health check endpoints
- Application Insights logging
- Basic API endpoints

### Future (v2.0+)

- Background job processing
- Distributed caching (Redis)
- Message queue (Azure Service Bus)
- Read replicas

## ✅ Quality Gates

### CI/CD Checks

Every PR must pass:

1. ✅ **Build & Test**: Code compiles
2. ✅ **Security Scan**: CodeQL passes
3. ✅ **Architecture**: Boundaries respected
4. ⚠️ **Code Quality**: Formatting (warning only)

### Architecture Metrics

- Lines per service: < 500
- Cyclomatic complexity: < 10
- Architecture violations: 0
- Security patterns: 100% compliant

## 🛠️ Maintenance

### Updating Documentation

When changing architecture:

1. Update relevant markdown files
2. Update Mermaid diagrams if structure changes
3. Create ADR if significant decision
4. Run validation scripts
5. Update metrics if needed

### Script Maintenance

Validation scripts in `scripts/`:

- Written in bash for portability
- Exit 0 = pass, Exit 1 = fail
- Colorized output for readability
- Fast execution (< 20 seconds)

## 📞 Support

- **Architecture Questions**: See `docs/architecture/`
- **Decision Rationale**: Check `docs/adr/`
- **Validation Issues**: Run scripts locally first
- **CI/CD Problems**: Check workflow logs

## 🎓 Learning Resources

1. **Start Here**: `docs/architecture/README.md`
2. **Understand System**: `architecture-overview.md`
3. **Visual Guide**: `c4-diagrams.md`
4. **Deploy It**: `deployment-architecture.md`
5. **Follow Rules**: `architecture-governance.md`

---

**Architecture as Code Benefits**:

✅ Version controlled with code  
✅ Living documentation stays current  
✅ Automated validation prevents drift  
✅ Visual diagrams aid communication  
✅ Decision history preserved  
✅ New team members onboard faster  
✅ Quality gates enforced automatically  

The architecture is now code - treat it with the same rigor as production code!
