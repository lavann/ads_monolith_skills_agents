# High-Level Design (HLD) — Retail Monolith

## System Overview

The Retail Monolith is an ASP.NET Core 9 web application implementing a basic e-commerce platform using the Razor Pages framework. The system provides a complete retail flow from product browsing to order completion.

**Architecture Pattern**: Monolithic web application  
**Target Framework**: .NET 9.0  
**Application Type**: Server-rendered Razor Pages with supplementary Minimal APIs

## Major Components

### 1. Web Application Layer (`Program.cs`, `Pages/`)
- **Entry Point**: `Program.cs` (lines 1-67)
- **Technology**: ASP.NET Core Razor Pages
- **Responsibilities**:
  - HTTP request handling
  - Server-side page rendering
  - Dependency injection configuration
  - Startup and middleware pipeline configuration

### 2. Service Layer (`Services/`)
- **ICartService / CartService**: Shopping cart operations (`Services/CartService.cs`)
- **ICheckoutService / CheckoutService**: Order processing orchestration (`Services/CheckoutService .cs`)
- **IPaymentGateway / MockPaymentGateway**: Payment processing abstraction (`Services/MockPaymentGateway.cs`)

### 3. Data Access Layer (`Data/`)
- **AppDbContext**: Entity Framework Core DbContext (`Data/AppDbContext.cs`)
- **DesignTimeDbContextFactory**: EF migrations tooling support (`Data/DesignTimeDbContextFactory.cs`)

### 4. Domain Models (`Models/`)
- **Product**: Product catalog entity (`Models/Product.cs`)
- **InventoryItem**: Stock tracking entity (`Models/InventoryItem.cs`)
- **Cart / CartLine**: Shopping cart entities (`Models/Cart.cs`)
- **Order / OrderLine**: Order entities (`Models/Order.cs`)

## Domain Boundaries

| Domain | Data Ownership | Business Logic | HTTP Surface Area | Dependencies |
|--------|---------------|----------------|-------------------|--------------|
| **Products** | `Products` table | Product listing, filtering | `Pages/Products/Index.cshtml.cs` | None |
| **Inventory** | `Inventory` table | Stock reservation, quantity checks | None (internal only) | Products (via SKU) |
| **Cart** | `Carts`, `CartLines` tables | Add to cart, retrieve cart, clear cart | `Pages/Cart/Index.cshtml.cs` | Products, CartService |
| **Checkout** | None (orchestrator) | Payment processing, order creation, stock decrement | `Pages/Checkout/Index.cshtml.cs`, `POST /api/checkout` | Cart, Orders, Inventory, PaymentGateway |
| **Orders** | `Orders`, `OrderLines` tables | Order persistence, order retrieval | `Pages/Orders/`, `GET /api/orders/{id}` | None (read-only consumer) |

## Data Stores

### Primary Database
- **Type**: SQL Server (LocalDB for development, SQL Server for production)
- **Connection**: Configured via `appsettings.json` or `appsettings.Development.json`
- **Default Connection String**: `Server=(localdb)\MSSQLLocalDB;Database=RetailMonolith;Trusted_Connection=True;MultipleActiveResultSets=true`
- **ORM**: Entity Framework Core 9.0.9
- **Migration Strategy**: Code-first with automatic migration on startup (`Program.cs:27`)

### Database Tables
- **Products**: Product catalog (Id, Sku, Name, Description, Price, Currency, IsActive, Category)
- **Inventory**: Stock levels (Id, Sku, Quantity)
- **Carts**: Active shopping carts (Id, CustomerId)
- **CartLines**: Cart line items (Id, CartId, Sku, Name, UnitPrice, Quantity)
- **Orders**: Completed orders (Id, CreatedUtc, CustomerId, Status, Total)
- **OrderLines**: Order line items (Id, OrderId, Sku, Name, UnitPrice, Quantity)

## External Dependencies

### NuGet Packages
1. **Microsoft.EntityFrameworkCore.SqlServer** (9.0.9)
   - Purpose: SQL Server database provider for Entity Framework Core
   - Used for: All database operations

2. **Microsoft.EntityFrameworkCore.Design** (9.0.9)
   - Purpose: EF Core design-time components
   - Used for: Database migrations tooling

3. **Microsoft.AspNetCore.Diagnostics.HealthChecks** (2.2.0)
   - Purpose: Health check endpoints
   - Used for: Operational monitoring (configured but not exposed in routing)

4. **Microsoft.Extensions.Http.Polly** (9.0.9)
   - Purpose: HTTP resilience and transient fault handling
   - Used for: Potential future HTTP client integrations (not currently utilized)

### External Services
- **MockPaymentGateway**: Simulated payment processor
  - Implementation: `Services/MockPaymentGateway.cs`
  - Behavior: Always returns success with mock transaction ID
  - Real-world replacement: Would integrate with Stripe, PayPal, or similar payment processor

## Runtime Assumptions

### Development Environment
- **Port**: HTTPS on 5001, HTTP on 5000 (Kestrel defaults)
- **Database**: LocalDB instance automatically created
- **Auto-migration**: Database schema created/updated on startup (`Program.cs:27`)
- **Auto-seeding**: 50 sample products inserted on first run (`Data/AppDbContext.cs:30-60`)

### Production Considerations
- **Environment Variable**: `ASPNETCORE_ENVIRONMENT` determines configuration source
- **Connection String**: Override via environment variable `ConnectionStrings__DefaultConnection`
- **Customer Identity**: Hardcoded to `"guest"` throughout (no authentication/authorization)
- **Session Management**: None (stateless with database-backed cart)
- **HTTPS Redirection**: Enabled (`Program.cs:40`)
- **HSTS**: Enabled in non-development environments (`Program.cs:37`)

### Startup Sequence
1. Application builder configured (`Program.cs:6`)
2. DbContext registered with SQL Server provider (`Program.cs:9-11`)
3. Services registered (scoped lifetime for CartService, CheckoutService, PaymentGateway) (`Program.cs:16-18`)
4. Health checks added but not mapped to endpoint (`Program.cs:19`)
5. Application built (`Program.cs:21`)
6. Database migrations applied automatically (`Program.cs:27`)
7. Database seeded if empty (`Program.cs:28`)
8. Middleware pipeline configured (`Program.cs:33-45`)
9. Razor Pages and Minimal APIs mapped (`Program.cs:47, 51-63`)
10. Application runs (`Program.cs:67`)

## Operational Characteristics

### Scalability Constraints
- Single SQL Server database (shared state)
- No distributed caching
- Synchronous processing model
- Hardcoded customer ID limits multi-tenancy

### Failure Modes
- **Database unavailability**: Application fails to start (auto-migration blocks startup)
- **Payment gateway failure**: Orders created with "Failed" status, but inventory already decremented
- **Concurrent stock reservation**: No optimistic concurrency control, potential overselling
- **Cart abandonment**: Carts persist indefinitely (no cleanup mechanism)

### Known Technical Debt
- **No authentication**: All requests use hardcoded `"guest"` customer ID
- **No session management**: Cart is customer-scoped but not session-scoped
- **Auto-migration on startup**: Unsafe for production (can cause downtime or data loss)
- **Mock payment gateway**: Not production-ready
- **No logging**: Minimal logging configuration (default ASP.NET Core logging only)
- **No monitoring/telemetry**: Health checks configured but not exposed
- **Inventory race conditions**: No pessimistic locking or optimistic concurrency tokens
- **Global error handling**: Basic exception handler, no structured error responses
