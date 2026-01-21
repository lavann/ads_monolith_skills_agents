# ADR-001: Monolithic Architecture with Shared Database

**Status**: Accepted (Implicit)  
**Date**: 2025-10-19 (inferred from initial migration date)  
**Context**: System Discovery Exercise

## Context

The Retail Monolith was built as a single ASP.NET Core web application with all domains (Products, Cart, Checkout, Orders, Inventory) deployed together in one process and sharing a single SQL Server database.

## Decision

Deploy all retail functionality as a monolithic application with:
- Single `AppDbContext` managing all entities
- All services registered in the same dependency injection container
- Shared database tables accessed by multiple domains
- Single deployment artifact (one executable)

## Evidence

- **Program.cs:9-11**: Single `AppDbContext` registered for the entire application
- **Data/AppDbContext.cs:13-19**: All DbSets (Products, Inventory, Carts, Orders) defined in one context
- **RetailMonolith.csproj**: Single project file with all code
- **Migrations/20251019185248_Initial.cs**: Single migration creating all tables together

## Consequences

### Positive
- **Simple deployment**: One artifact to build, test, and deploy
- **Simple transactions**: All database operations use the same DbContext, enabling single transactions across domains (e.g., CheckoutService)
- **Low operational complexity**: No service discovery, no distributed tracing, no inter-service communication
- **Fast local development**: Single database, single process, easy to debug
- **Shared code**: Common models (e.g., SKU) reused across domains without duplication

### Negative
- **Cannot scale domains independently**: If checkout is a bottleneck, must scale entire app
- **Shared database contention**: All domains compete for connection pool and locks
- **Tight coupling**: Changes to one domain can impact others (e.g., schema changes)
- **Single point of failure**: If any domain crashes, entire app goes down
- **Difficult to decompose later**: Shared database creates coupling that must be untangled for microservices migration

## Alternatives Considered

(None apparent in codebase; this was the initial design)

## Notes

The monolithic structure is appropriate for the current scale and complexity. The codebase shows some preparation for future decomposition:
- Service interfaces (`ICartService`, `ICheckoutService`, `IPaymentGateway`)
- Logical domain boundaries (separate folders for Models, Services, Pages)
- Minimal APIs (`POST /api/checkout`, `GET /api/orders/{id}`) that could become standalone services

However, the shared database and lack of domain events create strong coupling that would need to be addressed before decomposition.
