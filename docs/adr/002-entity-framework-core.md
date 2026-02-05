# ADR-002: Use Entity Framework Core for Data Access

## Status
**Accepted**

**Date**: 2026-02-05

## Context

The application needs to persist data including:
- User accounts and authentication information
- Product catalog with images
- Crawler configuration
- Audit logs

We need to choose a data access strategy that:
- Works well with SQL Server
- Supports migrations for schema evolution
- Provides type safety
- Integrates with ASP.NET Core
- Simplifies CRUD operations

## Decision

We will use **Entity Framework Core 9.0** as our ORM (Object-Relational Mapper) for all data access operations.

EF Core will be used with:
- Code-First approach for schema definition
- Migrations for database versioning
- DbContext as Unit of Work pattern
- LINQ for queries
- Change tracking for updates

## Alternatives Considered

### Alternative 1: Dapper (Micro ORM)
- **Pros**: 
  - Faster query execution
  - More control over SQL
  - Smaller footprint
  - Good for read-heavy scenarios
- **Cons**: 
  - Manual mapping required
  - No automatic migrations
  - More boilerplate code
  - No change tracking
- **Why not chosen**: Need migrations and prefer productivity over raw speed

### Alternative 2: ADO.NET (Raw SQL)
- **Pros**: 
  - Complete control
  - Maximum performance
  - No abstraction overhead
- **Cons**: 
  - Verbose code
  - Manual SQL injection prevention
  - No migrations
  - Type safety only at runtime
  - Connection management complexity
- **Why not chosen**: Too much boilerplate, error-prone, slow development

### Alternative 3: NHibernate
- **Pros**: 
  - Mature ORM
  - Rich feature set
  - Good performance
- **Cons**: 
  - Steeper learning curve
  - XML configuration heavy (historically)
  - Less ASP.NET Core integration
  - Smaller community vs EF Core
- **Why not chosen**: EF Core is more idiomatic for .NET Core

## Consequences

### Positive Consequences
- ✅ **Code-First**: Define schema in C# classes
- ✅ **Migrations**: Automatic schema versioning and deployment
- ✅ **Type Safety**: Compile-time checking for queries
- ✅ **LINQ**: Familiar query syntax
- ✅ **Integration**: First-class ASP.NET Core support
- ✅ **Identity Integration**: Works seamlessly with ASP.NET Core Identity
- ✅ **Productivity**: Less boilerplate, faster development

### Negative Consequences
- ❌ **Performance**: Slower than raw ADO.NET or Dapper
- ❌ **N+1 queries**: Easy to introduce inefficient queries
- ❌ **Memory**: Change tracking adds overhead
- ❌ **Complex queries**: Some scenarios require raw SQL
- ❌ **Learning curve**: Understanding EF Core internals for optimization

### Neutral Consequences
- ⚪ Need to understand when queries execute (lazy loading)
- ⚪ Must configure relationships explicitly
- ⚪ Periodic migrations as schema evolves

### Risks
- ⚠️ **Query performance**: Inefficient LINQ queries
  - *Mitigation*: Use `.AsNoTracking()` for reads, profile queries, use projections
- ⚠️ **Migration conflicts**: Team members creating conflicting migrations
  - *Mitigation*: Coordinate schema changes, use feature branches
- ⚠️ **Connection leaks**: Improper DbContext lifetime
  - *Mitigation*: Use DI with scoped lifetime, always dispose

## Implementation Notes

1. **DbContext Configuration**:
   ```csharp
   builder.Services.AddDbContext<ApplicationDbContext>(options =>
       options.UseSqlServer(connectionString));
   ```

2. **Entity Configuration**:
   ```csharp
   public class Product
   {
       public int Id { get; set; }
       public string ArticleNumber { get; set; }
       // ... other properties
   }
   ```

3. **Migration Commands**:
   ```bash
   dotnet ef migrations add MigrationName
   dotnet ef database update
   ```

4. **Best Practices**:
   - Use `AsNoTracking()` for read-only queries
   - Eager load related entities with `Include()`
   - Use projections to select only needed columns
   - Configure indexes in `OnModelCreating()`
   - Use `FromSqlRaw()` for complex queries when needed

5. **Performance Optimization**:
   - Enable query splitting for large includes
   - Use compiled queries for frequently executed queries
   - Batch inserts/updates when possible
   - Monitor SQL with logging in development

## References

- [EF Core Documentation](https://learn.microsoft.com/en-us/ef/core/)
- [EF Core Performance](https://learn.microsoft.com/en-us/ef/core/performance/)
- [EF Core Migrations](https://learn.microsoft.com/en-us/ef/core/managing-schemas/migrations/)
- [Best Practices](https://learn.microsoft.com/en-us/ef/core/miscellaneous/configuring-dbcontext)

## Revision History

| Date | Author | Change |
|------|--------|--------|
| 2026-02-05 | System | Initial version based on implementation |
