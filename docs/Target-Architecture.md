# Target Architecture — Retail Monolith Modernisation

**Status**: Proposed  
**Date**: 2025-01-21  
**Authors**: Modernisation Planning Team

## Executive Summary

This document defines the target architecture for modernising the Retail Monolith from a single monolithic ASP.NET Core application into a containerised microservices architecture. The modernisation follows a **strangler fig pattern**, extracting services incrementally while maintaining behaviour preservation and rollback capability at each phase.

## Current State Summary

The existing system is a monolithic ASP.NET Core 9 Razor Pages application with:
- Single SQL Server database shared across all domains
- Five logical domains: Products, Inventory, Cart, Checkout, Orders
- No authentication (hardcoded "guest" customer)
- Auto-migration on startup
- Mock payment gateway
- Direct database access from UI layer (bypassing service layer in places)
- Critical inventory-payment race condition in checkout flow

## Target Architecture Overview

### High-Level Architecture

```
┌─────────────────────────────────────────────────────────────────┐
│                        API Gateway / BFF                         │
│                    (YARP or Azure API Management)                │
└────────┬────────────────────┬─────────────────┬─────────────────┘
         │                    │                 │
         │                    │                 │
    ┌────▼────┐         ┌─────▼──────┐    ┌────▼──────┐
    │ Product │         │  Checkout  │    │   Order   │
    │ Service │         │  Service   │    │  Service  │
    └────┬────┘         └─────┬──────┘    └────┬──────┘
         │                    │                 │
         │              ┌─────▼──────┐         │
         │              │ Inventory  │         │
         │              │  Service   │         │
         │              └─────┬──────┘         │
         │                    │                 │
    ┌────▼────────────────────▼─────────────────▼──────┐
    │            Shared Database (Phase 1-3)            │
    │              (SQL Server Container)               │
    └───────────────────────────────────────────────────┘
                         │
                         │ (Phase 4+)
                         ▼
    ┌──────────┐  ┌──────────┐  ┌──────────┐  ┌──────────┐
    │ Products │  │ Inventory│  │   Cart   │  │  Orders  │
    │    DB    │  │    DB    │  │    DB    │  │    DB    │
    └──────────┘  └──────────┘  └──────────┘  └──────────┘
```

### Key Principles

1. **Incremental Migration**: Extract one service at a time using strangler fig pattern
2. **Behaviour Preservation**: No functionality changes during migration
3. **Rollback Capability**: Each phase must be independently reversible
4. **Shared Database Initially**: Maintain single database through phases 1-3 to reduce risk
5. **API-First**: All services expose RESTful HTTP APIs (ASP.NET Core Minimal APIs)
6. **Container-Based**: All services deployed as Docker containers
7. **Service Independence**: Services can be deployed, scaled, and versioned independently

## Service Boundaries

### 1. Product Service

**Responsibility**: Product catalog management and browsing

**Domain Entities**:
- Product (Id, Sku, Name, Description, Price, Currency, IsActive, Category)

**API Endpoints**:
- `GET /api/products` - List all active products
- `GET /api/products/{id}` - Get single product by ID
- `GET /api/products/by-sku/{sku}` - Get product by SKU

**Data Access**: 
- Phase 1-3: Shared database (Products table)
- Phase 4+: Dedicated Products database

**Dependencies**: None

**Technology Stack**:
- ASP.NET Core 9 Minimal APIs
- Entity Framework Core 9
- SQL Server (LocalDB for dev, SQL Server for prod)

### 2. Inventory Service

**Responsibility**: Stock level tracking and reservation

**Domain Entities**:
- InventoryItem (Id, Sku, Quantity)

**API Endpoints**:
- `GET /api/inventory/{sku}` - Get stock level for SKU
- `POST /api/inventory/{sku}/reserve` - Reserve inventory for order (idempotent)
- `POST /api/inventory/{sku}/release` - Release reserved inventory (rollback)
- `POST /api/inventory/{sku}/commit` - Commit reservation (finalize)

**Data Access**:
- Phase 1-3: Shared database (Inventory table)
- Phase 4+: Dedicated Inventory database

**Dependencies**: None (internal only, called by Checkout Service)

**Key Design Decision**: Introduce **two-phase inventory reservation** to fix the existing race condition:
1. **Reserve**: Lock inventory without decrementing (pre-payment)
2. **Commit**: Decrement inventory after successful payment
3. **Release**: Rollback if payment fails

### 3. Cart Service

**Responsibility**: Shopping cart management

**Domain Entities**:
- Cart (Id, CustomerId)
- CartLine (Id, CartId, Sku, Name, UnitPrice, Quantity)

**API Endpoints**:
- `GET /api/carts/{customerId}` - Get cart with lines
- `POST /api/carts/{customerId}/items` - Add item to cart
- `DELETE /api/carts/{customerId}` - Clear cart

**Data Access**:
- Phase 1-3: Shared database (Carts, CartLines tables)
- Phase 4+: Dedicated Cart database (possibly Redis for ephemeral carts)

**Dependencies**: 
- Product Service (validate product exists, get current price)

**Migration Note**: Cart Service remains embedded in monolith initially (not extracted in first slice)

### 4. Order Service

**Responsibility**: Order persistence and retrieval

**Domain Entities**:
- Order (Id, CreatedUtc, CustomerId, Status, Total)
- OrderLine (Id, OrderId, Sku, Name, UnitPrice, Quantity)

**API Endpoints**:
- `GET /api/orders` - List all orders for customer
- `GET /api/orders/{id}` - Get single order with lines
- `POST /api/orders` - Create new order (called by Checkout Service)

**Data Access**:
- Phase 1-3: Shared database (Orders, OrderLines tables)
- Phase 4+: Dedicated Orders database

**Dependencies**: None (read-only consumer)

### 5. Checkout Service (Orchestrator)

**Responsibility**: Orchestrate payment processing and order creation

**API Endpoints**:
- `POST /api/checkout` - Process checkout (cart → payment → order)

**Orchestration Flow**:
```
1. Retrieve cart (Cart Service)
2. Validate inventory (Inventory Service - reserve)
3. Charge payment (Payment Gateway)
4. Commit inventory (Inventory Service - commit OR release)
5. Create order (Order Service)
6. Clear cart (Cart Service)
7. Return order
```

**Dependencies**:
- Cart Service
- Inventory Service
- Payment Gateway (external)
- Order Service

**Data Access**: None (orchestrator only, no direct database access)

**Key Fix**: Implement **saga pattern** with compensating transactions:
- If payment fails after inventory reserve → release inventory
- If order creation fails → refund payment, release inventory
- All steps must be idempotent and logged for audit

### 6. Web Frontend (BFF - Backend for Frontend)

**Responsibility**: Server-side rendering of Razor Pages and routing to backend services

**Technology**: ASP.NET Core 9 Razor Pages

**API Calls**: HTTP calls to backend services via API Gateway

**Deployment**: Separate container from backend services

**Migration Path**:
- Phase 1-2: Remains monolithic, hosts all Razor Pages
- Phase 3+: Gradually convert to API consumer (pages call backend services)
- Phase 4+: Consider SPA (React/Vue) or continue Razor Pages as BFF

## Container Deployment Model

### Docker Compose (Development)

```yaml
version: '3.8'
services:
  sqlserver:
    image: mcr.microsoft.com/mssql/server:2022-latest
    environment:
      - SA_PASSWORD=YourStrong@Passw0rd
      - ACCEPT_EULA=Y
    ports:
      - "1433:1433"
    volumes:
      - sqldata:/var/opt/mssql

  product-service:
    build: ./src/ProductService
    environment:
      - ConnectionStrings__DefaultConnection=Server=sqlserver;Database=RetailMonolith;User=sa;Password=YourStrong@Passw0rd;TrustServerCertificate=True
    ports:
      - "5001:8080"
    depends_on:
      - sqlserver

  order-service:
    build: ./src/OrderService
    environment:
      - ConnectionStrings__DefaultConnection=Server=sqlserver;Database=RetailMonolith;User=sa;Password=YourStrong@Passw0rd;TrustServerCertificate=True
    ports:
      - "5002:8080"
    depends_on:
      - sqlserver

  api-gateway:
    build: ./src/ApiGateway
    ports:
      - "5000:8080"
    depends_on:
      - product-service
      - order-service

  web-frontend:
    build: ./src/WebFrontend
    environment:
      - ApiGateway__BaseUrl=http://api-gateway:8080
    ports:
      - "5003:8080"
    depends_on:
      - api-gateway

volumes:
  sqldata:
```

### Kubernetes Deployment (Production)

Each service deployed as:
- **Deployment**: 2+ replicas for high availability
- **Service**: ClusterIP for internal communication
- **Ingress**: HTTPS routing via NGINX or Azure Application Gateway
- **ConfigMap**: Environment-specific configuration
- **Secret**: Database connection strings, API keys

Example structure:
```
k8s/
├── namespace.yaml
├── sqlserver/
│   ├── deployment.yaml
│   ├── service.yaml
│   ├── pvc.yaml
│   └── secret.yaml
├── product-service/
│   ├── deployment.yaml
│   ├── service.yaml
│   └── configmap.yaml
├── order-service/
│   ├── deployment.yaml
│   ├── service.yaml
│   └── configmap.yaml
├── api-gateway/
│   ├── deployment.yaml
│   ├── service.yaml
│   └── ingress.yaml
└── web-frontend/
    ├── deployment.yaml
    ├── service.yaml
    └── configmap.yaml
```

## Routing and API Gateway

### Technology Options

| Option | Pros | Cons | Recommendation |
|--------|------|------|----------------|
| **YARP (Yet Another Reverse Proxy)** | .NET native, easy integration, free | Less mature than alternatives | **Recommended for Phase 1-2** |
| **Azure API Management** | Enterprise features, built-in analytics, rate limiting | Cost, complexity, cloud lock-in | Consider for Phase 3+ |
| **Ocelot** | .NET specific, good documentation | Discontinued support | Not recommended |
| **NGINX** | Battle-tested, high performance | Configuration complexity, not .NET native | Alternative option |

### YARP Configuration Example

```json
{
  "ReverseProxy": {
    "Routes": {
      "products-route": {
        "ClusterId": "product-service",
        "Match": {
          "Path": "/api/products/{**catch-all}"
        }
      },
      "orders-route": {
        "ClusterId": "order-service",
        "Match": {
          "Path": "/api/orders/{**catch-all}"
        }
      }
    },
    "Clusters": {
      "product-service": {
        "Destinations": {
          "destination1": {
            "Address": "http://product-service:8080"
          }
        }
      },
      "order-service": {
        "Destinations": {
          "destination1": {
            "Address": "http://order-service:8080"
          }
        }
      }
    }
  }
}
```

### Gateway Responsibilities

1. **Routing**: Forward requests to appropriate backend service
2. **Authentication**: Validate JWT tokens (when auth is added)
3. **Rate Limiting**: Protect backend services from abuse
4. **Logging**: Centralized request/response logging
5. **Monitoring**: Health checks, metrics collection
6. **CORS**: Manage cross-origin requests for future SPA

## Data Access Strategy

### Phase 1-3: Shared Database

**Rationale**: Minimise migration risk by maintaining single database

**Approach**:
- All services connect to same SQL Server instance
- Each service uses its own DbContext with subset of entities
- Schema remains unchanged (existing tables)
- No foreign keys across service boundaries (SKU remains string-based)
- Each service responsible for its own data integrity

**Example - Product Service DbContext**:
```csharp
public class ProductDbContext : DbContext
{
    public DbSet<Product> Products { get; set; }
    
    // Only Products table exposed to this service
    // No access to Orders, Carts, etc.
}
```

**Advantages**:
- ✅ Faster migration (no data movement)
- ✅ Existing transactions still work
- ✅ Easy rollback (just route traffic back)
- ✅ No data synchronization issues

**Disadvantages**:
- ❌ Services not fully independent (shared database = shared fate)
- ❌ Schema changes require coordination across services
- ❌ Cannot scale database per service needs

### Phase 4+: Database per Service

**Rationale**: Achieve true service independence

**Approach**:
- Extract data from shared database into service-specific databases
- Implement data synchronization for cross-service queries
- Use event-driven patterns for eventual consistency
- Introduce saga coordinator for distributed transactions

**Migration Strategy**:
1. **Dual Writes**: Write to both old and new database (temporary)
2. **Data Backfill**: Copy historical data to new database
3. **Read Switch**: Switch reads to new database, continue dual writes
4. **Write Switch**: Stop writing to old database
5. **Cleanup**: Remove old tables from shared database

**Example - Order Service with Dedicated DB**:
- Orders DB contains: Orders, OrderLines
- Product names/prices denormalized into OrderLine (snapshot at order time)
- No joins to Products table (data captured at checkout)

### Data Consistency Patterns

#### Pattern 1: Event-Driven Updates
- Product price changes publish `ProductPriceChanged` event
- Other services subscribe and update their local denormalized copies
- Eventual consistency acceptable for most use cases

#### Pattern 2: API Composition
- Gateway aggregates data from multiple services
- Frontend makes multiple API calls and merges results
- Works for real-time data needs (e.g., current product details + order history)

#### Pattern 3: CQRS (Command Query Responsibility Segregation)
- Separate read models optimized for queries
- Write models (commands) maintain transactional consistency
- Read models updated asynchronously via events

## Configuration Management

### Environment-Specific Settings

**Development (docker-compose.yml)**:
- LocalDB or SQL Server container
- In-memory caching
- Verbose logging
- Auto-migration enabled

**Staging (Kubernetes ConfigMaps)**:
- Azure SQL Database
- Redis cache
- Standard logging
- Manual migrations (via init container)

**Production (Azure Key Vault + ConfigMaps)**:
- Azure SQL Database (geo-replicated)
- Redis cache (Azure Cache for Redis)
- Minimal logging (errors only)
- Manual migrations with approval gate

### Configuration Sources Priority

1. Environment variables (highest priority)
2. Kubernetes ConfigMaps/Secrets
3. appsettings.{Environment}.json
4. appsettings.json (lowest priority)

### Secret Management

**Development**: Local secrets via user-secrets (dotnet user-secrets)

**Production**: 
- Azure Key Vault for connection strings, API keys
- Managed identities for Azure resources (no connection strings needed)
- Kubernetes secrets for non-Azure deployments

## Observability and Monitoring

### Logging

**Technology**: Serilog + Seq (dev) or Application Insights (prod)

**Structured Logging Example**:
```csharp
_logger.LogInformation(
    "Order {OrderId} created for customer {CustomerId} with total {Total}",
    order.Id, order.CustomerId, order.Total
);
```

### Metrics

**Technology**: Prometheus + Grafana (open-source) or Application Insights

**Key Metrics**:
- Request count per service
- Response time percentiles (p50, p95, p99)
- Error rate per endpoint
- Database connection pool usage
- Cart abandonment rate

### Distributed Tracing

**Technology**: OpenTelemetry + Jaeger (dev) or Application Insights

**Trace Context Propagation**:
- W3C Trace Context standard
- Correlate requests across service boundaries
- Track checkout flow from frontend → checkout → inventory → payment → order

### Health Checks

**Per-Service Health Endpoint**: `GET /health`

**Checks**:
- Database connectivity
- Downstream service availability (for orchestrators)
- Memory/CPU thresholds

**Kubernetes Integration**:
- Liveness probe: Container alive (restart if fails)
- Readiness probe: Ready to accept traffic (remove from load balancer if fails)

## Authentication and Authorization (Future State)

**Current State**: Hardcoded "guest" customer (no auth)

**Target State**: JWT-based authentication

### Recommended Approach

**Option 1: Azure AD B2C** (Cloud-native)
- External identity provider
- Social logins (Google, Facebook, Microsoft)
- No password management required

**Option 2: ASP.NET Core Identity** (Self-hosted)
- Built-in user management
- Local database for users
- Full control over auth flow

### Implementation Strategy

1. **Phase 1**: Add session-based customer ID (GUID in cookie) - removes hardcoded "guest"
2. **Phase 2**: Implement authentication in API Gateway
3. **Phase 3**: Propagate user context to backend services (JWT in Authorization header)
4. **Phase 4**: Add role-based authorization (customer, admin, support)

### JWT Token Structure

```json
{
  "sub": "customer-12345",
  "email": "user@example.com",
  "role": "customer",
  "iat": 1705852800,
  "exp": 1705856400
}
```

## Migration and Deployment Strategy

### Strangler Fig Pattern

1. **Identify seam**: Choose a bounded context to extract (e.g., Orders)
2. **Create new service**: Implement as separate microservice
3. **Route new requests**: API Gateway routes traffic to new service
4. **Monolith calls service**: Monolith uses HTTP client for extracted domain
5. **Decommission monolith code**: Remove old code once validated
6. **Repeat**: Extract next service

### Deployment Pipeline

```
┌──────────┐    ┌──────────┐    ┌──────────┐    ┌──────────┐
│  Build   │───▶│   Test   │───▶│  Deploy  │───▶│  Verify  │
│          │    │          │    │  Staging │    │  Smoke   │
└──────────┘    └──────────┘    └─────┬────┘    └──────────┘
                                      │
                                      │ Manual Approval
                                      │
                                 ┌────▼─────┐    ┌──────────┐
                                 │  Deploy  │───▶│  Monitor │
                                 │   Prod   │    │   APM    │
                                 └──────────┘    └──────────┘
```

### CI/CD Tools

**GitHub Actions** (Recommended for this project)
- Native GitHub integration
- Free for public repos
- YAML-based pipeline definitions

**Azure DevOps Pipelines** (Alternative)
- Enterprise features
- Better for Azure deployments

### Blue/Green Deployment

1. Deploy new version to "green" environment
2. Run smoke tests against green
3. Switch traffic from blue to green (DNS/load balancer update)
4. Monitor for issues
5. Rollback to blue if problems detected
6. Decommission blue after stabilization period

## Risk Mitigation

### Technical Risks

| Risk | Likelihood | Impact | Mitigation |
|------|-----------|--------|------------|
| **Service communication failures** | Medium | High | Circuit breakers, retry policies, fallback responses |
| **Database migration errors** | Medium | Critical | Manual migrations with rollback scripts, test on staging first |
| **Performance degradation (network hops)** | Low | Medium | Response time monitoring, caching, async patterns |
| **Data inconsistency across services** | Medium | High | Event sourcing, saga pattern, compensating transactions |
| **Incomplete rollback** | Low | Critical | Feature flags, blue/green deployment, database backups |

### Operational Risks

| Risk | Likelihood | Impact | Mitigation |
|------|-----------|--------|------------|
| **Increased operational complexity** | High | Medium | Invest in observability, runbooks, automated deployments |
| **Team skill gaps (Docker/K8s)** | Medium | Medium | Training, pair programming, gradual adoption |
| **Cost increase (cloud resources)** | Low | Low | Start with minimal resources, scale as needed, cost monitoring |

## Success Criteria

### Technical Metrics

- ✅ All services deployable independently
- ✅ No shared code between services (except contracts)
- ✅ Database per service (Phase 4+)
- ✅ End-to-end tests passing
- ✅ Response time p95 < 500ms per service
- ✅ Error rate < 0.1% per endpoint

### Business Metrics

- ✅ Zero downtime during migration
- ✅ No functionality changes (behaviour preservation)
- ✅ Ability to scale services independently
- ✅ Reduced time to deploy changes (per service)

## Next Steps

1. **Review and approve** this target architecture
2. **Create Migration Plan** (see `/docs/Migration-Plan.md`)
3. **Create ADRs** for key decisions (service decomposition, API gateway, data strategy)
4. **Execute Phase 1** (extract first service - see Migration Plan for details)

## References

- [Martin Fowler - Microservices](https://martinfowler.com/articles/microservices.html)
- [Sam Newman - Monolith to Microservices](https://samnewman.io/books/monolith-to-microservices/)
- [Microsoft - .NET Microservices Architecture](https://docs.microsoft.com/en-us/dotnet/architecture/microservices/)
- [Strangler Fig Pattern](https://martinfowler.com/bliki/StranglerFigApplication.html)
