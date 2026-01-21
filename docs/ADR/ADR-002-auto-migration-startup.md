# ADR-002: Auto-Migration and Seeding on Application Startup

**Status**: Accepted (Implicit)  
**Date**: 2025-10-19 (inferred from initial migration date)  
**Context**: System Discovery Exercise

## Context

Database schema initialization and sample data seeding are required for development and demo environments. The application needs a way to ensure the database is ready when the app starts.

## Decision

Automatically apply Entity Framework Core migrations and seed sample data during application startup, before the HTTP pipeline begins processing requests.

## Evidence

**Program.cs:24-29**:
```csharp
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.MigrateAsync(); // <-- Auto-migration
    await AppDbContext.SeedAsync(db); // <-- Auto-seeding
}
```

**Data/AppDbContext.cs:30-60**: `SeedAsync` method checks if Products table is empty and inserts 50 sample products with inventory.

## Consequences

### Positive
- **Zero-config local development**: Developers can run the app without manual database setup
- **Demo-ready**: Fresh deployments automatically have sample data for demonstrations
- **Simplified CI/CD**: No separate database initialization step required in pipelines
- **Self-healing**: If database is dropped, app recreates schema on next startup

### Negative
- **Unsafe for production**: Running migrations on startup can cause:
  - Downtime while migrations execute (blocks request handling)
  - Data loss if migration is destructive (DROP COLUMN, etc.)
  - Concurrent migration conflicts in multi-instance deployments (e.g., Kubernetes with replicas)
  - Startup failures if migration fails (app won't start)
- **Slow startup**: Migration execution adds latency to app initialization
- **No rollback mechanism**: Failed migrations leave database in inconsistent state
- **Seeding pollution**: `SeedAsync` only checks if Products table is empty, not if specific products already exist (could cause duplicate SKU errors if seeding logic changes)

## Risks Identified

1. **Production Deployment Risk**: If this pattern is used in production, a bad migration could take down the entire application until manually resolved.

2. **Multi-Instance Race Condition**: In a load-balanced or orchestrated environment (e.g., Azure App Service with multiple instances, Kubernetes with replicas), multiple instances may attempt to run migrations simultaneously, causing:
   - Deadlocks
   - Duplicate migration entries
   - Inconsistent schema state

3. **Database Lock Contention**: Migration operations often require exclusive locks (e.g., `ALTER TABLE`), which can block other instances from starting.

## Alternatives Considered

(None apparent in codebase; this was the default pattern used)

**Recommended Alternatives**:
- **Manual migrations**: Run `dotnet ef database update` as part of deployment pipeline, before app starts
- **Database initialization container**: In Kubernetes, use an init container to run migrations before app pods start
- **Feature flags**: Environment variable to disable auto-migration in production (`if (Environment.IsDevelopment()) { ... }`)

## Notes

The current implementation is acceptable for:
- Local development (single developer, LocalDB)
- Demo/sandbox environments (ephemeral, easily recreated)

It is **NOT safe** for:
- Production environments
- Multi-instance deployments (horizontal scaling)
- Environments with strict uptime requirements

The comment on **Program.cs:8** acknowledges this: `"DB � localdb for hack; swap to SQL in appsettings for Azure"`, suggesting awareness that this pattern is temporary/development-only.

However, there is no conditional logic to prevent auto-migration in production (no check for `app.Environment.IsProduction()`).
