# Architecture Decision Records (ADR)

An Architecture Decision Record (ADR) captures a significant architectural decision along with its context and consequences.

## What is an ADR?

An ADR is a document that captures an important architectural decision made along with its context and consequences. It helps teams:

- Understand why decisions were made
- Avoid revisiting settled decisions
- Onboard new team members
- Learn from past decisions

## ADR Format

Each ADR follows this structure:

```markdown
# ADR-XXX: Title

## Status
Proposed | Accepted | Deprecated | Superseded

## Context
What is the issue we're seeing that is motivating this decision or change?

## Decision
What is the change that we're proposing and/or doing?

## Consequences
What becomes easier or more difficult to do because of this change?

### Positive
- Good things

### Negative  
- Trade-offs

### Risks
- Potential issues
```

## Current ADRs

| ADR | Title | Status | Date |
|-----|-------|--------|------|
| [001](001-blazor-server-architecture.md) | Use Blazor Server for UI | Accepted | 2026-02-05 |
| [002](002-entity-framework-core.md) | Use Entity Framework Core for Data Access | Accepted | 2026-02-05 |
| [003](003-aspnet-core-identity.md) | Use ASP.NET Core Identity | Accepted | 2026-02-05 |
| [004](004-sql-server-database.md) | Use SQL Server as Primary Database | Accepted | 2026-02-05 |
| [005](005-middleware-security-architecture.md) | Middleware-Based Security Architecture | Accepted | 2026-02-05 |
| [006](006-abot-web-crawler.md) | Use Abot2 for Web Crawling | Accepted | 2026-02-05 |
| [007](007-configuration-strategy.md) | Configuration Management Strategy | Accepted | 2026-02-05 |

## Creating New ADRs

When making a significant architectural decision:

1. Copy the template from `adr-template.md`
2. Number it sequentially (next available number)
3. Fill in all sections with clear reasoning
4. Submit for review in PR
5. Update this index once accepted

## Superseding ADRs

When an ADR is superseded:

1. Mark old ADR status as "Superseded by ADR-XXX"
2. Create new ADR explaining the change
3. Keep old ADR for historical context
