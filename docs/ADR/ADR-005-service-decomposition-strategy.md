# ADR-005: Service Decomposition Strategy

**Status**: Proposed  
**Date**: 2025-01-21  
**Context**: Modernisation Planning

## Context

The Retail Monolith needs to be decomposed into microservices to achieve:
- Independent deployability per domain
- Horizontal scalability per service
- Technology flexibility per service
- Reduced blast radius of failures

The system has five logical domains: Products, Inventory, Cart, Checkout, and Orders. We must decide which domains become services and in what order to extract them.

## Decision

Adopt **Domain-Driven Design (DDD) bounded contexts** as service boundaries with **strangler fig migration pattern**:

1. **Service Boundaries**:
   - **Product Service**: Product catalog (read-only, no dependencies)
   - **Order Service**: Order persistence (read-only after creation, no dependencies)
   - **Cart Service**: Shopping cart management (depends on Product Service)
   - **Inventory Service**: Stock reservation (internal only, called by Checkout)
   - **Checkout Service**: Orchestrator (saga coordinator, no database)

2. **Extraction Order**:
   - **Phase 1**: Order Service (read-only, lowest risk)
   - **Phase 2**: Product Service (read-only, foundational)
   - **Phase 3**: Cart, Inventory, Checkout Services (write-heavy, orchestrated)

3. **Architectural Patterns**:
   - **API Gateway**: YARP (reverse proxy) for routing
   - **Saga Pattern**: Checkout Service orchestrates distributed transactions
   - **Shared Database**: Phases 1-3 use shared SQL Server
   - **Database per Service**: Phase 4+ (after services stabilize)

## Rationale

### Why These Service Boundaries?

**Products**: 
- Clear bounded context (catalog management)
- High read volume → benefits from independent caching/scaling
- No business logic complexity

**Orders**:
- Natural aggregate root (Order + OrderLines)
- Immutable after creation (no updates)
- No dependencies on other services

**Cart**:
- Ephemeral, session-scoped data
- Clear lifecycle (create → populate → checkout → delete)
- Depends on Products for validation

**Inventory**:
- Critical consistency requirements (prevent overselling)
- Internal service (not exposed to frontend)
- Tightly coupled to Checkout flow

**Checkout**:
- Pure orchestrator (no data ownership)
- Coordinates Cart, Inventory, Payment, Order
- Implements saga pattern for distributed transactions

### Why This Order?

**Order Service First**:
- ✅ Read-only (no write-side complexity)
- ✅ No dependencies (can run in isolation)
- ✅ Low risk (failure doesn't block sales)
- ✅ Visible value (demonstrates pattern)

**Product Service Second**:
- ✅ Read-only (low risk)
- ✅ Foundational (other services depend on it)
- ✅ High read volume (benefits from caching)

**Cart/Inventory/Checkout Together**:
- ✅ Tightly coupled in checkout flow
- ✅ Requires distributed transaction coordination
- ✅ Best extracted together to avoid partial states

### Why Not Other Approaches?

**Alternative 1: Extract all services at once ("big bang")**
- ❌ High risk (cannot rollback individual services)
- ❌ Long development cycle (no incremental value)
- ❌ Difficult to test (too many moving parts)

**Alternative 2: Extract by technical layer (UI, service, data)**
- ❌ Doesn't align with business domains
- ❌ Doesn't provide independent deployability
- ❌ Creates distributed monolith

**Alternative 3: Extract by feature (e.g., "checkout flow")**
- ❌ Crosscuts multiple domains
- ❌ Doesn't provide clear service boundaries
- ❌ Duplicates logic across services

## Consequences

### Positive

- ✅ **Clear ownership**: Each service owns a bounded context
- ✅ **Independent deployability**: Services can be deployed without coordination
- ✅ **Incremental migration**: Low risk, reversible at each phase
- ✅ **Testability**: Services can be tested in isolation
- ✅ **Scalability**: High-traffic services (Products) can scale independently

### Negative

- ❌ **Increased complexity**: 5+ services vs 1 monolith
- ❌ **Network latency**: HTTP calls between services add overhead
- ❌ **Distributed transactions**: Saga pattern more complex than local transactions
- ❌ **Operational overhead**: More deployments, more monitoring
- ❌ **Team coordination**: Changes spanning multiple services require coordination

### Risks

| Risk | Likelihood | Impact | Mitigation |
|------|-----------|--------|------------|
| **Service boundaries change** | Medium | High | Keep services loosely coupled, use versioned APIs |
| **Performance degradation** | Low | Medium | Add caching, monitor response times, optimize queries |
| **Saga failures** | Medium | High | Implement compensating transactions, extensive testing |
| **Operational complexity** | High | Medium | Invest in observability, automation, runbooks |

## Alternatives Considered

### Alternative 1: Keep Monolith, Scale Horizontally

**Pros**: Simple, proven, low risk  
**Cons**: Cannot scale domains independently, all-or-nothing deployments

**Rejected**: Doesn't achieve goal of independent deployability

### Alternative 2: Serverless Functions (Azure Functions / AWS Lambda)

**Pros**: Auto-scaling, pay-per-use, no infrastructure management  
**Cons**: Cold start latency, vendor lock-in, complex local development

**Rejected**: Team lacks serverless experience, prefer containers

### Alternative 3: Event-Driven Architecture (Pub/Sub)

**Pros**: Loose coupling, asynchronous processing, scalability  
**Cons**: Eventual consistency, complex debugging, event versioning

**Deferred**: Consider after Phase 4 (database decomposition) if needed

## Implementation Notes

### Service Communication

**Synchronous**: HTTP/REST for request-response (e.g., Get Product, Get Order)  
**Asynchronous**: Event bus (e.g., RabbitMQ, Azure Service Bus) for notifications (Phase 5+)

### API Contracts

**Use OpenAPI/Swagger** for all service APIs:
```csharp
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

app.MapSwagger();
app.MapSwaggerUI();
```

**Versioning Strategy**: URL path versioning (`/api/v1/products`, `/api/v2/products`)

### Error Handling

**Return problem details (RFC 7807)**:
```csharp
app.MapGet("/api/products/{id}", async (int id, ProductDbContext db) =>
{
    var product = await db.Products.FindAsync(id);
    return product is null 
        ? Results.Problem("Product not found", statusCode: 404)
        : Results.Ok(product);
});
```

### Correlation IDs

**Propagate correlation ID across service calls** for distributed tracing:
```csharp
client.DefaultRequestHeaders.Add("X-Correlation-Id", Activity.Current?.Id);
```

## Validation

### Success Criteria

- ✅ Each service deployable independently via CI/CD
- ✅ No shared code between services (except contracts/DTOs)
- ✅ Each service has clear bounded context (no overlap)
- ✅ End-to-end tests pass (checkout flow)
- ✅ Response time p95 < 500ms per service
- ✅ Rollback tested and documented per phase

### Acceptance Tests

**Phase 1 (Order Service)**:
- User can view order details via Order Service API
- Monolith creates orders via Order Service POST endpoint
- Order Service failure degrades gracefully (error page, not crash)

**Phase 2 (Product Service)**:
- Product browsing loads from Product Service
- Cart uses Product Service to validate SKUs
- Product Service cached responses (Redis)

**Phase 3 (Checkout Services)**:
- Checkout saga completes successfully
- Failed payment releases inventory (compensating transaction)
- Distributed trace shows all service calls

## References

- [Domain-Driven Design - Eric Evans](https://www.domainlanguage.com/ddd/)
- [Building Microservices - Sam Newman](https://samnewman.io/books/building_microservices/)
- [Microservices Patterns - Chris Richardson](https://microservices.io/patterns/)
- [Strangler Fig Pattern - Martin Fowler](https://martinfowler.com/bliki/StranglerFigApplication.html)

## Related ADRs

- ADR-006: Data Migration Strategy (Shared DB → Database per Service)
- ADR-007: API Gateway Technology Selection (YARP)
- ADR-008: Saga Pattern for Distributed Transactions
