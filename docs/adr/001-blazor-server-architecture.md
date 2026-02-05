# ADR-001: Use Blazor Server for UI Architecture

## Status
**Accepted**

**Date**: 2026-02-05

## Context

The application needs a modern, interactive web UI for managing products. We need to choose between:
- Traditional MVC with JavaScript
- Single Page Application (React, Angular, Vue)
- Blazor WebAssembly
- Blazor Server

Requirements:
- Real-time updates for crawler progress
- Secure authentication
- Fast initial load time
- Minimal client-side complexity
- Strong typing and code sharing between client/server

## Decision

We will use **Blazor Server** as the UI architecture for the Product Manager application.

Blazor Server uses SignalR to maintain a persistent connection between browser and server, executing .NET code on the server and sending UI updates to the client over this connection.

## Alternatives Considered

### Alternative 1: Blazor WebAssembly
- **Pros**: 
  - Runs entirely in browser (offline capable)
  - Reduces server load
  - Can be hosted on static file servers
- **Cons**: 
  - Larger initial download (WebAssembly runtime)
  - No server-side prerendering out of box
  - More complex authentication
  - No real-time capabilities without additional setup
- **Why not chosen**: Need server-side security and real-time updates; initial load size is a concern

### Alternative 2: React/Angular SPA
- **Pros**: 
  - Large ecosystem and community
  - Mature tooling
  - More developers familiar with it
- **Cons**: 
  - Requires separate API layer
  - Different languages (C# backend, JS frontend)
  - No code sharing
  - More complex security (JWT, CORS)
- **Why not chosen**: Want to use C# throughout; avoid maintaining separate API

### Alternative 3: Traditional MVC + jQuery
- **Pros**: 
  - Proven technology
  - Simple deployment
  - No WebSocket dependencies
- **Cons**: 
  - Not truly interactive
  - Requires full page reloads
  - Mixing server and client code awkwardly
  - No real-time updates without polling
- **Why not chosen**: Need modern interactive UX with real-time updates

## Consequences

### Positive Consequences
- ✅ **Single language**: C# for both frontend and backend
- ✅ **Code sharing**: Share models, validation, and utilities
- ✅ **Real-time**: Built-in SignalR for live updates during crawling
- ✅ **Strong typing**: Compile-time checking throughout
- ✅ **Security**: Server-side execution, no exposed API surface
- ✅ **Fast initial load**: Only HTML and small JS shim needed
- ✅ **Simple deployment**: Single ASP.NET Core application

### Negative Consequences
- ❌ **Server resources**: Each user maintains SignalR connection
- ❌ **Scalability**: More challenging to scale horizontally
- ❌ **Network dependency**: Requires stable connection
- ❌ **Latency**: Every interaction requires server round-trip
- ❌ **State management**: Server maintains circuit state per user

### Neutral Consequences
- ⚪ Need to configure SignalR timeouts appropriately
- ⚪ Must handle reconnection scenarios
- ⚪ Server-side rendering by default

### Risks
- ⚠️ **Connection stability**: Users on poor networks may have issues
  - *Mitigation*: Configure appropriate timeouts and show connection status
- ⚠️ **Memory consumption**: Each user's circuit held in memory
  - *Mitigation*: Implement circuit cleanup, monitor server resources
- ⚠️ **Horizontal scaling**: SignalR requires sticky sessions
  - *Mitigation*: Use Azure SignalR Service or Redis backplane when scaling

## Implementation Notes

1. Configure Blazor Server in `Program.cs`:
   ```csharp
   builder.Services.AddRazorComponents()
       .AddInteractiveServerComponents();
   ```

2. Use Fluent UI components for consistent design:
   ```csharp
   builder.Services.AddFluentUIComponents();
   ```

3. Configure SignalR settings:
   - Circuit timeout: 30 seconds
   - Disconnect timeout: 3 minutes
   - Max buffer size: 32KB

4. Implement connection status UI to inform users

5. Use streaming rendering where appropriate for better UX

## References

- [Blazor Server Documentation](https://learn.microsoft.com/en-us/aspnet/core/blazor/hosting-models#blazor-server)
- [SignalR Configuration](https://learn.microsoft.com/en-us/aspnet/core/blazor/fundamentals/signalr)
- [Blazor Performance Best Practices](https://learn.microsoft.com/en-us/aspnet/core/blazor/performance)

## Revision History

| Date | Author | Change |
|------|--------|--------|
| 2026-02-05 | System | Initial version based on implementation |
