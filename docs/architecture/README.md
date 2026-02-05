# Architecture Documentation

This directory contains architectural documentation for the Product Manager application.

## Contents

- **[Architecture Overview](architecture-overview.md)** - High-level system architecture
- **[C4 Diagrams](c4-diagrams.md)** - Context, Container, Component, and Code diagrams
- **[Deployment Architecture](deployment-architecture.md)** - Infrastructure and deployment strategy
- **[ADRs](../adr/)** - Architecture Decision Records

## Architecture as Code Philosophy

This project follows the "Architecture as Code" approach:

1. **Version Controlled** - All architecture artifacts are in git
2. **Living Documentation** - Updated alongside code changes
3. **Diagrams as Code** - Using Mermaid for maintainable diagrams
4. **Decision Records** - ADRs document key architectural decisions
5. **Automated Validation** - Architecture tests verify constraints

## Key Architectural Principles

1. **Security First** - Multiple layers of security (auth, headers, rate limiting)
2. **Separation of Concerns** - Clear boundaries between layers
3. **Dependency Injection** - Loose coupling, testability
4. **Configuration Over Code** - Settings externalized via appsettings/secrets
5. **Blazor Server Pattern** - Real-time UI with SignalR
6. **Entity Framework Core** - Code-first database approach

## Technology Stack

- **Framework**: ASP.NET Core 9.0
- **UI**: Blazor Server with Fluent UI
- **Database**: SQL Server with EF Core
- **Authentication**: ASP.NET Core Identity
- **Web Crawler**: Abot2
- **CI/CD**: GitHub Actions

## How to Update

When making architectural changes:

1. Update relevant documentation in this directory
2. Create an ADR if it's a significant decision
3. Update diagrams if system structure changes
4. Run architecture tests to validate constraints
5. Review documentation in PR process
