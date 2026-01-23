# Migration Plan — Retail Monolith to Microservices

**Status**: Proposed  
**Date**: 2025-01-21  
**Authors**: Modernisation Planning Team

## Executive Summary

This document outlines the incremental migration plan for transforming the Retail Monolith from a single ASP.NET Core application into a containerised microservices architecture. The migration follows a **strangler fig pattern**, extracting services one at a time with rollback capability at each phase.

**Total Duration**: 8-12 weeks (depending on team velocity)  
**Team Size**: 2-3 developers + 1 DevOps engineer  
**Risk Level**: Low to Medium (increases in later phases)

## Migration Principles

1. **Incremental Progress**: Extract one service per phase
2. **Behaviour Preservation**: No new features during migration
3. **Continuous Delivery**: Deploy to production after each phase
4. **Rollback First**: Design rollback before implementing forward migration
5. **Measure Everything**: Monitor performance and errors at each phase
6. **Shared Database Initially**: Defer data migration until services stabilize

## Phase Overview

| Phase | Service Extracted | Duration | Risk | Rollback Complexity |
|-------|------------------|----------|------|-------------------|
| **Phase 0** | Foundation & Containerisation | 1-2 weeks | Low | N/A (prep work) |
| **Phase 1** | Order Service | 1-2 weeks | Low | Low (read-only) |
| **Phase 2** | Product Service | 1-2 weeks | Low | Low (read-only) |
| **Phase 3** | Inventory & Checkout Services | 2-3 weeks | Medium | Medium (write operations) |
| **Phase 4** | Database Decomposition | 2-3 weeks | High | High (data migration) |
| **Phase 5** | Frontend Modernisation (Optional) | 2-4 weeks | Medium | Medium (UI changes) |

---

## Phase 0: Foundation & Containerisation

**Goal**: Establish infrastructure for microservices deployment without extracting services

**Duration**: 1-2 weeks

### Objectives

1. ✅ Containerise the existing monolith
2. ✅ Set up Docker Compose for local development
3. ✅ Create Kubernetes manifests for production deployment
4. ✅ Establish CI/CD pipeline
5. ✅ Add observability (logging, metrics, tracing)
6. ✅ Fix critical technical debt (inventory-payment race condition)

### Tasks

#### 1. Containerise Monolith

**Create `Dockerfile` in project root**:
```dockerfile
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY ["RetailMonolith.csproj", "./"]
RUN dotnet restore
COPY . .
RUN dotnet build -c Release -o /app/build

FROM build AS publish
RUN dotnet publish -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "RetailMonolith.dll"]
```

**Test**:
```bash
docker build -t retail-monolith:latest .
docker run -p 5000:8080 retail-monolith:latest
```

#### 2. Create Docker Compose Configuration

**Create `docker-compose.yml`**:
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
    healthcheck:
      test: /opt/mssql-tools/bin/sqlcmd -S localhost -U sa -P YourStrong@Passw0rd -Q "SELECT 1"
      interval: 10s
      timeout: 5s
      retries: 5

  monolith:
    build: .
    environment:
      - ASPNETCORE_ENVIRONMENT=Development
      - ConnectionStrings__DefaultConnection=Server=sqlserver;Database=RetailMonolith;User=sa;Password=YourStrong@Passw0rd;TrustServerCertificate=True
    ports:
      - "5000:8080"
    depends_on:
      sqlserver:
        condition: service_healthy

volumes:
  sqldata:
```

**Test**:
```bash
docker-compose up -d
curl http://localhost:5000/Products
docker-compose down
```

#### 3. Set Up CI/CD Pipeline

**Create `.github/workflows/build-and-test.yml`**:
```yaml
name: Build and Test

on:
  push:
    branches: [ main ]
  pull_request:
    branches: [ main ]

jobs:
  build:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - name: Setup .NET
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '9.0.x'
      - name: Restore dependencies
        run: dotnet restore
      - name: Build
        run: dotnet build --no-restore
      - name: Test
        run: dotnet test --no-build --verbosity normal
      - name: Build Docker image
        run: docker build -t retail-monolith:${{ github.sha }} .
```

#### 4. Add Observability

**Install NuGet packages**:
```bash
dotnet add package Serilog.AspNetCore
dotnet add package Serilog.Sinks.Console
dotnet add package Serilog.Sinks.Seq
dotnet add package OpenTelemetry.Extensions.Hosting
dotnet add package OpenTelemetry.Instrumentation.AspNetCore
dotnet add package OpenTelemetry.Instrumentation.Http
```

**Update `Program.cs`** to add structured logging:
```csharp
using Serilog;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .WriteTo.Seq("http://localhost:5341")
    .CreateLogger();

builder.Host.UseSerilog();
```

**Add health check endpoint**:
```csharp
app.MapHealthChecks("/health");
```

#### 5. Fix Inventory-Payment Race Condition

**Problem**: Inventory decremented before payment charged (CheckoutService.cs:27-36)

**Solution**: Reverse the order:
1. Charge payment first
2. Decrement inventory only if payment succeeds
3. Add compensating transaction to release inventory if later steps fail

**This is a bug fix, not a feature**: Required for safe migration

#### 6. Remove Auto-Migration on Startup

**Problem**: `Program.cs:27` runs migrations automatically (unsafe for production)

**Solution**: 
- Add feature flag: `builder.Configuration.GetValue<bool>("AutoMigration", false)`
- Only enable in Development environment
- Create manual migration script for production

### Success Criteria

- ✅ Monolith runs in Docker container locally
- ✅ Docker Compose starts all services successfully
- ✅ CI/CD pipeline builds and tests on every push
- ✅ Logging outputs to console and Seq
- ✅ Health check endpoint returns 200 OK
- ✅ Inventory-payment bug fixed (payment before inventory decrement)

### Rollback

Not applicable (no production changes yet)

---

## Phase 1: Extract Order Service (First Slice)

**Goal**: Extract read-only Order Service as proof of concept

**Duration**: 1-2 weeks  
**Risk**: Low (read-only service, no writes)  
**Rollback Complexity**: Low

### Why Order Service First?

1. **Read-Only**: Orders are write-once, read-many (no updates after creation)
2. **No Dependencies**: Order Service has no downstream dependencies
3. **Clear Boundary**: Order domain is well-isolated
4. **Low Risk**: If service fails, monolith can still serve data
5. **Visible Value**: Demonstrates microservices pattern with minimal risk

### Architecture

```
┌─────────────┐
│   Monolith  │
│  (Razor     │
│   Pages)    │
└──────┬──────┘
       │
       ├─────────────┐
       │             │
       ▼             ▼
┌──────────┐   ┌──────────────┐
│  Orders  │   │   Products   │
│  (via    │   │   Cart       │
│  HTTP)   │   │   Checkout   │
└────┬─────┘   │  (embedded)  │
     │         └──────┬───────┘
     │                │
     └────────┬───────┘
              │
         ┌────▼─────┐
         │   SQL    │
         │  Server  │
         └──────────┘
```

### Tasks

#### 1. Create Order Service Project

**Directory structure**:
```
src/
├── RetailMonolith/          # Existing monolith
└── OrderService/
    ├── OrderService.csproj
    ├── Program.cs
    ├── Models/
    │   ├── Order.cs
    │   └── OrderLine.cs
    ├── Data/
    │   └── OrderDbContext.cs
    └── Dockerfile
```

**Create `OrderService.csproj`**:
```xml
<Project Sdk="Microsoft.NET.Sdk.Web">
  <PropertyGroup>
    <TargetFramework>net9.0</TargetFramework>
    <Nullable>enable</Nullable>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.EntityFrameworkCore.SqlServer" Version="9.0.9" />
    <PackageReference Include="Serilog.AspNetCore" Version="8.0.3" />
  </ItemGroup>
</Project>
```

**Copy models from monolith**: `Order.cs`, `OrderLine.cs` (no changes)

**Create `OrderDbContext.cs`** (subset of AppDbContext):
```csharp
public class OrderDbContext : DbContext
{
    public DbSet<Order> Orders { get; set; }
    public DbSet<OrderLine> OrderLines { get; set; }
    
    // Only expose Orders/OrderLines, not Products/Carts
}
```

#### 2. Implement Order API

**Create `Program.cs`** with Minimal APIs:
```csharp
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<OrderDbContext>(o =>
    o.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

var app = builder.Build();

// GET /api/orders
app.MapGet("/api/orders", async (OrderDbContext db, string? customerId) =>
{
    var query = db.Orders.Include(o => o.Lines).AsQueryable();
    
    if (!string.IsNullOrEmpty(customerId))
        query = query.Where(o => o.CustomerId == customerId);
    
    var orders = await query
        .OrderByDescending(o => o.CreatedUtc)
        .ToListAsync();
    
    return Results.Ok(orders);
});

// GET /api/orders/{id}
app.MapGet("/api/orders/{id:int}", async (int id, OrderDbContext db) =>
{
    var order = await db.Orders
        .Include(o => o.Lines)
        .SingleOrDefaultAsync(o => o.Id == id);
    
    return order is null ? Results.NotFound() : Results.Ok(order);
});

// POST /api/orders (called by CheckoutService)
app.MapPost("/api/orders", async (CreateOrderRequest req, OrderDbContext db) =>
{
    var order = new Order
    {
        CustomerId = req.CustomerId,
        Status = req.Status,
        Total = req.Total,
        Lines = req.Lines.Select(l => new OrderLine
        {
            Sku = l.Sku,
            Name = l.Name,
            UnitPrice = l.UnitPrice,
            Quantity = l.Quantity
        }).ToList()
    };
    
    db.Orders.Add(order);
    await db.SaveChangesAsync();
    
    return Results.Created($"/api/orders/{order.Id}", order);
});

app.MapHealthChecks("/health");

app.Run();

public record CreateOrderRequest(
    string CustomerId,
    string Status,
    decimal Total,
    List<CreateOrderLineRequest> Lines
);

public record CreateOrderLineRequest(
    string Sku,
    string Name,
    decimal UnitPrice,
    int Quantity
);
```

#### 3. Update Monolith to Call Order Service

**Add HttpClient in `Program.cs`**:
```csharp
builder.Services.AddHttpClient("OrderService", client =>
{
    client.BaseAddress = new Uri(builder.Configuration["OrderService:BaseUrl"] 
        ?? "http://localhost:5002");
});
```

**Update `Pages/Orders/Index.cshtml.cs`** to call Order Service:
```csharp
private readonly IHttpClientFactory _httpClientFactory;

public async Task OnGetAsync()
{
    var client = _httpClientFactory.CreateClient("OrderService");
    Orders = await client.GetFromJsonAsync<List<Order>>("/api/orders?customerId=guest")
        ?? new List<Order>();
}
```

**Update `Pages/Orders/Details.cshtml.cs`** similarly

**Update `CheckoutService.CheckoutAsync`** to POST to Order Service:
```csharp
// Replace: db.Orders.Add(order); await db.SaveChangesAsync();
// With: HTTP POST to OrderService
var client = _httpClientFactory.CreateClient("OrderService");
var response = await client.PostAsJsonAsync("/api/orders", new CreateOrderRequest(...));
var order = await response.Content.ReadFromJsonAsync<Order>();
```

#### 4. Update Docker Compose

**Add Order Service**:
```yaml
services:
  # ... existing services ...

  order-service:
    build:
      context: ./src/OrderService
    environment:
      - ASPNETCORE_ENVIRONMENT=Development
      - ConnectionStrings__DefaultConnection=Server=sqlserver;Database=RetailMonolith;User=sa;Password=YourStrong@Passw0rd;TrustServerCertificate=True
    ports:
      - "5002:8080"
    depends_on:
      - sqlserver

  monolith:
    # ... existing config ...
    environment:
      - OrderService__BaseUrl=http://order-service:8080
    depends_on:
      - sqlserver
      - order-service  # Add dependency
```

#### 5. Test End-to-End

**Test scenarios**:
1. Browse products → Add to cart → Checkout → **View order details** (via Order Service)
2. Direct API call: `GET http://localhost:5002/api/orders/1`
3. Verify monolith logs show HTTP calls to Order Service
4. Stop Order Service → Verify graceful degradation (error message, not crash)

### Success Criteria

- ✅ Order Service runs independently in Docker container
- ✅ Monolith successfully calls Order Service via HTTP
- ✅ Order details page displays data from Order Service
- ✅ Checkout flow creates orders via Order Service API
- ✅ Response time < 300ms for GET /api/orders/{id}
- ✅ No errors in logs during normal operation

### Rollback Plan

**Scenario**: Order Service has critical bugs or performance issues

**Rollback Steps**:
1. Update monolith configuration: `OrderService__BaseUrl=disabled`
2. Revert `Pages/Orders/Index.cshtml.cs` to direct database access (Git revert)
3. Revert `CheckoutService.cs` to direct database writes
4. Redeploy monolith
5. Stop Order Service container

**Rollback Time**: < 10 minutes (configuration change + redeploy)

**Data Impact**: None (Order Service only reads/writes same database tables)

### Lessons Learned Checkpoint

**After Phase 1, review**:
- Was HTTP communication overhead acceptable?
- Did logging/tracing provide adequate visibility?
- Were error messages clear enough for debugging?
- Did team velocity match estimates?

---

## Phase 2: Extract Product Service

**Goal**: Extract read-only Product Service

**Duration**: 1-2 weeks  
**Risk**: Low (read-only, no business logic)  
**Rollback Complexity**: Low

### Architecture

```
┌─────────────┐
│   Monolith  │
└──────┬──────┘
       │
       ├───────────┬────────────┐
       │           │            │
       ▼           ▼            ▼
┌──────────┐ ┌──────────┐ ┌──────────┐
│ Products │ │  Orders  │ │   Cart   │
│ Service  │ │ Service  │ │ Checkout │
└────┬─────┘ └────┬─────┘ │(embedded)│
     │            │        └────┬─────┘
     │            │             │
     └────────────┴─────────────┘
                  │
             ┌────▼─────┐
             │   SQL    │
             │  Server  │
             └──────────┘
```

### Tasks

#### 1. Create Product Service

**Similar structure to Order Service**:
```
src/ProductService/
├── ProductService.csproj
├── Program.cs
├── Models/
│   └── Product.cs
├── Data/
│   └── ProductDbContext.cs
└── Dockerfile
```

**API Endpoints**:
- `GET /api/products` - List all active products
- `GET /api/products/{id}` - Get single product
- `GET /api/products/by-sku/{sku}` - Get product by SKU (for cart/checkout)

#### 2. Update Monolith

**Update `Pages/Products/Index.cshtml.cs`**:
```csharp
// Replace: Products = await _db.Products.Where(p => p.IsActive).ToListAsync();
// With: HTTP call to ProductService
var client = _httpClientFactory.CreateClient("ProductService");
Products = await client.GetFromJsonAsync<List<Product>>("/api/products") ?? new();
```

**Update `CartService.AddToCartAsync`**:
```csharp
// Replace: var product = await _db.Products.FindAsync(productId);
// With: HTTP call to ProductService
var client = _httpClientFactory.CreateClient("ProductService");
var product = await client.GetFromJsonAsync<Product>($"/api/products/{productId}");
```

#### 3. Add Caching Layer

**Problem**: Product data read frequently (every page load)

**Solution**: Add distributed cache (Redis) or in-memory cache

**Install Redis** (add to docker-compose.yml):
```yaml
redis:
  image: redis:7-alpine
  ports:
    - "6379:6379"
```

**Add caching in Product Service**:
```csharp
builder.Services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = builder.Configuration.GetConnectionString("Redis");
});

app.MapGet("/api/products", async (ProductDbContext db, IDistributedCache cache) =>
{
    var cached = await cache.GetStringAsync("products:all");
    if (cached != null)
        return Results.Ok(JsonSerializer.Deserialize<List<Product>>(cached));
    
    var products = await db.Products.Where(p => p.IsActive).ToListAsync();
    await cache.SetStringAsync("products:all", 
        JsonSerializer.Serialize(products), 
        new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5) });
    
    return Results.Ok(products);
});
```

### Success Criteria

- ✅ Product Service runs independently
- ✅ Product browsing page loads from Product Service
- ✅ Cart adds items using Product Service API
- ✅ Response time < 200ms (with caching)
- ✅ Cache hit rate > 80%

### Rollback Plan

Same approach as Phase 1: configuration change + Git revert + redeploy

---

## Phase 3: Extract Inventory & Checkout Services

**Goal**: Extract write-heavy services with orchestration

**Duration**: 2-3 weeks  
**Risk**: Medium (write operations, distributed transactions)  
**Rollback Complexity**: Medium

### Architecture

```
┌─────────────┐
│  Frontend   │
│  (Monolith  │
│   Razor)    │
└──────┬──────┘
       │
       ├───────┬──────────┬────────────┬──────────┐
       │       │          │            │          │
       ▼       ▼          ▼            ▼          ▼
  ┌────────┐ ┌────────┐ ┌────────┐ ┌──────────┐ ┌─────────┐
  │Product │ │ Order  │ │  Cart  │ │Inventory │ │Checkout │
  │Service │ │Service │ │Service │ │ Service  │ │ Service │
  └───┬────┘ └───┬────┘ └───┬────┘ └────┬─────┘ └────┬────┘
      │          │          │            │            │
      └──────────┴──────────┴────────────┴────────────┘
                            │
                       ┌────▼─────┐
                       │   SQL    │
                       │  Server  │
                       └──────────┘
```

### Key Challenge: Distributed Transactions

**Problem**: Checkout flow requires coordination across multiple services:
1. Reserve inventory (Inventory Service)
2. Charge payment (Payment Gateway)
3. Commit inventory (Inventory Service)
4. Create order (Order Service)
5. Clear cart (Cart Service)

**Solution**: Implement **Saga Orchestration Pattern**

### Tasks

#### 1. Create Inventory Service

**API Endpoints**:
- `POST /api/inventory/{sku}/reserve` - Reserve stock (idempotent, returns reservation ID)
- `POST /api/inventory/{sku}/commit` - Finalize reservation (decrement stock)
- `POST /api/inventory/{sku}/release` - Cancel reservation (rollback)
- `GET /api/inventory/{sku}` - Get current available quantity

**Reservation Model**:
```csharp
public class InventoryReservation
{
    public Guid Id { get; set; }  // Idempotency key
    public string Sku { get; set; }
    public int Quantity { get; set; }
    public DateTime ReservedAt { get; set; }
    public DateTime? ExpiresAt { get; set; }  // Auto-release after 10 minutes
    public string Status { get; set; }  // Reserved, Committed, Released
}
```

**Implementation**:
```csharp
app.MapPost("/api/inventory/{sku}/reserve", async (
    string sku, 
    int quantity, 
    Guid reservationId,  // Idempotency key from caller
    InventoryDbContext db) =>
{
    // Check for existing reservation (idempotency)
    var existing = await db.Reservations.FindAsync(reservationId);
    if (existing != null)
        return Results.Ok(existing);  // Already reserved
    
    // Check stock availability
    var inventory = await db.Inventory.SingleAsync(i => i.Sku == sku);
    var reserved = await db.Reservations
        .Where(r => r.Sku == sku && r.Status == "Reserved")
        .SumAsync(r => r.Quantity);
    
    var available = inventory.Quantity - reserved;
    if (available < quantity)
        return Results.Conflict("Insufficient stock");
    
    // Create reservation
    var reservation = new InventoryReservation
    {
        Id = reservationId,
        Sku = sku,
        Quantity = quantity,
        ReservedAt = DateTime.UtcNow,
        ExpiresAt = DateTime.UtcNow.AddMinutes(10),
        Status = "Reserved"
    };
    
    db.Reservations.Add(reservation);
    await db.SaveChangesAsync();
    
    return Results.Ok(reservation);
});

app.MapPost("/api/inventory/{sku}/commit", async (
    string sku, 
    Guid reservationId, 
    InventoryDbContext db) =>
{
    var reservation = await db.Reservations.FindAsync(reservationId);
    if (reservation == null || reservation.Status != "Reserved")
        return Results.BadRequest("Invalid reservation");
    
    // Decrement inventory
    var inventory = await db.Inventory.SingleAsync(i => i.Sku == sku);
    inventory.Quantity -= reservation.Quantity;
    
    reservation.Status = "Committed";
    await db.SaveChangesAsync();
    
    return Results.Ok();
});

app.MapPost("/api/inventory/{sku}/release", async (
    string sku, 
    Guid reservationId, 
    InventoryDbContext db) =>
{
    var reservation = await db.Reservations.FindAsync(reservationId);
    if (reservation == null)
        return Results.Ok();  // Idempotent
    
    reservation.Status = "Released";
    await db.SaveChangesAsync();
    
    return Results.Ok();
});
```

#### 2. Create Checkout Service (Orchestrator)

**Extract from monolith into standalone service**

**API Endpoint**:
- `POST /api/checkout` - Process checkout with saga orchestration

**Saga Implementation**:
```csharp
app.MapPost("/api/checkout", async (
    string customerId,
    string paymentToken,
    IHttpClientFactory httpClientFactory,
    IPaymentGateway paymentGateway,
    ILogger<Program> logger) =>
{
    var cartClient = httpClientFactory.CreateClient("CartService");
    var inventoryClient = httpClientFactory.CreateClient("InventoryService");
    var orderClient = httpClientFactory.CreateClient("OrderService");
    
    var sagaId = Guid.NewGuid();  // Correlation ID for tracing
    var reservations = new List<Guid>();
    
    logger.LogInformation("Starting checkout saga {SagaId} for customer {CustomerId}", 
        sagaId, customerId);
    
    try
    {
        // Step 1: Get cart
        var cart = await cartClient.GetFromJsonAsync<Cart>(
            $"/api/carts/{customerId}");
        
        if (cart == null || !cart.Lines.Any())
            return Results.BadRequest("Cart is empty");
        
        var total = cart.Lines.Sum(l => l.UnitPrice * l.Quantity);
        
        // Step 2: Reserve inventory (all or nothing)
        foreach (var line in cart.Lines)
        {
            var reservationId = Guid.NewGuid();
            var response = await inventoryClient.PostAsJsonAsync(
                $"/api/inventory/{line.Sku}/reserve",
                new { quantity = line.Quantity, reservationId });
            
            if (!response.IsSuccessStatusCode)
            {
                // Rollback: Release all previous reservations
                logger.LogWarning("Inventory reservation failed for {Sku}, rolling back", 
                    line.Sku);
                await ReleaseReservations(inventoryClient, reservations);
                return Results.Conflict("Insufficient stock");
            }
            
            reservations.Add(reservationId);
        }
        
        // Step 3: Charge payment
        var paymentResult = await paymentGateway.ChargeAsync(
            new(total, "GBP", paymentToken));
        
        if (!paymentResult.Succeeded)
        {
            // Rollback: Release inventory
            logger.LogWarning("Payment failed, rolling back reservations");
            await ReleaseReservations(inventoryClient, reservations);
            return Results.BadRequest("Payment failed: " + paymentResult.Error);
        }
        
        // Step 4: Commit inventory (payment succeeded)
        foreach (var line in cart.Lines.Zip(reservations))
        {
            await inventoryClient.PostAsync(
                $"/api/inventory/{line.First.Sku}/commit?reservationId={line.Second}", 
                null);
        }
        
        // Step 5: Create order
        var orderResponse = await orderClient.PostAsJsonAsync("/api/orders", new
        {
            customerId,
            status = "Paid",
            total,
            lines = cart.Lines.Select(l => new
            {
                l.Sku,
                l.Name,
                l.UnitPrice,
                l.Quantity
            })
        });
        
        var order = await orderResponse.Content.ReadFromJsonAsync<Order>();
        
        // Step 6: Clear cart
        await cartClient.DeleteAsync($"/api/carts/{customerId}");
        
        logger.LogInformation("Checkout saga {SagaId} completed successfully, order {OrderId}", 
            sagaId, order.Id);
        
        return Results.Ok(order);
    }
    catch (Exception ex)
    {
        // Rollback: Release all reservations
        logger.LogError(ex, "Checkout saga {SagaId} failed with exception", sagaId);
        await ReleaseReservations(inventoryClient, reservations);
        
        // Note: Payment already charged - manual refund required
        // In production: Call payment gateway refund API
        
        return Results.Problem("Checkout failed: " + ex.Message);
    }
});

async Task ReleaseReservations(HttpClient client, List<Guid> reservationIds)
{
    foreach (var reservationId in reservationIds)
    {
        try
        {
            await client.PostAsync($"/api/inventory/release?reservationId={reservationId}", 
                null);
        }
        catch (Exception ex)
        {
            // Log but don't fail (best effort rollback)
            logger.LogError(ex, "Failed to release reservation {ReservationId}", 
                reservationId);
        }
    }
}
```

#### 3. Create Cart Service

**Extracted from monolith `CartService.cs`**

**API Endpoints**:
- `GET /api/carts/{customerId}` - Get cart with lines
- `POST /api/carts/{customerId}/items` - Add item to cart
- `DELETE /api/carts/{customerId}` - Clear cart

**Implementation**: Mirror existing `CartService` logic as HTTP endpoints

#### 4. Update Frontend

**Pages call services via HTTP**:
- Products page → Product Service
- Cart page → Cart Service
- Checkout page → Checkout Service (saga orchestrator)
- Orders page → Order Service

### Success Criteria

- ✅ Checkout flow completes successfully end-to-end
- ✅ Inventory reserved before payment, committed after
- ✅ Failed payments release inventory (no stock loss)
- ✅ Saga compensating transactions work (rollback tested)
- ✅ Distributed tracing correlates requests across services
- ✅ Response time < 1000ms for complete checkout (acceptable with multiple HTTP hops)

### Rollback Plan

**Scenario**: Saga orchestration has bugs, inventory inconsistency

**Rollback Steps**:
1. Route checkout traffic back to monolith (feature flag)
2. Stop Checkout Service, Inventory Service containers
3. Revert monolith `CheckoutService.cs` (Git revert)
4. Manually reconcile inventory discrepancies (SQL script)

**Rollback Time**: 30-60 minutes (requires manual data verification)

**Prevention**: Extensive testing of compensating transactions before production deployment

---

## Phase 4: Database Decomposition

**Goal**: Separate shared database into service-specific databases

**Duration**: 2-3 weeks  
**Risk**: High (data migration, consistency challenges)  
**Rollback Complexity**: High

### Architecture

```
┌─────────────┐
│  Frontend   │
└──────┬──────┘
       │
       ├───────┬──────────┬────────────┬──────────┐
       │       │          │            │          │
       ▼       ▼          ▼            ▼          ▼
  ┌────────┐ ┌────────┐ ┌────────┐ ┌──────────┐ ┌─────────┐
  │Product │ │ Order  │ │  Cart  │ │Inventory │ │Checkout │
  └───┬────┘ └───┬────┘ └───┬────┘ └────┬─────┘ └────┬────┘
      │          │          │            │            │
  ┌───▼───┐  ┌───▼───┐  ┌───▼───┐  ┌────▼─────┐     │ (No DB)
  │Product│  │ Order │  │  Cart │  │Inventory │     │
  │  DB   │  │  DB   │  │  DB   │  │    DB    │     │
  └───────┘  └───────┘  └───────┘  └──────────┘     │
      │          │          │            │            │
      └──────────┴──────────┴────────────┴────────────┘
                  Event Bus (Optional)
```

### Challenges

1. **Foreign Key References**: SKU strings join Products and Inventory
2. **Distributed Queries**: Can't join across databases
3. **Data Consistency**: Eventual consistency vs strong consistency
4. **Migration Risk**: Data loss during migration

### Migration Strategy: Dual Writes

#### Step 1: Create New Databases

**Create separate databases**:
- `RetailMonolith_Products`
- `RetailMonolith_Orders`
- `RetailMonolith_Cart`
- `RetailMonolith_Inventory`

**Migrate schema**:
```sql
-- Products DB
CREATE TABLE Products (
    Id INT PRIMARY KEY IDENTITY,
    Sku NVARCHAR(50) NOT NULL UNIQUE,
    Name NVARCHAR(200) NOT NULL,
    Description NVARCHAR(MAX),
    Price DECIMAL(18,2) NOT NULL,
    Currency NVARCHAR(10) NOT NULL,
    IsActive BIT NOT NULL,
    Category NVARCHAR(50)
);

-- Orders DB
CREATE TABLE Orders (
    Id INT PRIMARY KEY IDENTITY,
    CreatedUtc DATETIME2 NOT NULL,
    CustomerId NVARCHAR(50) NOT NULL,
    Status NVARCHAR(50) NOT NULL,
    Total DECIMAL(18,2) NOT NULL
);

CREATE TABLE OrderLines (
    Id INT PRIMARY KEY IDENTITY,
    OrderId INT NOT NULL FOREIGN KEY REFERENCES Orders(Id),
    Sku NVARCHAR(50) NOT NULL,  -- Denormalized, no FK to Products
    Name NVARCHAR(200) NOT NULL,
    UnitPrice DECIMAL(18,2) NOT NULL,
    Quantity INT NOT NULL
);

-- Similar for Cart and Inventory
```

#### Step 2: Dual Write Phase

**Update services to write to BOTH databases**:
```csharp
// Example: Product Service
app.MapPost("/api/products", async (
    Product product, 
    ProductDbContext newDb,  // New dedicated DB
    AppDbContext oldDb)       // Old shared DB
{
    // Write to new database
    newDb.Products.Add(product);
    await newDb.SaveChangesAsync();
    
    // Write to old database (for rollback safety)
    oldDb.Products.Add(product);
    await oldDb.SaveChangesAsync();
    
    return Results.Created($"/api/products/{product.Id}", product);
});
```

**Duration**: 1 week (ensure all writes go to both databases)

#### Step 3: Backfill Historical Data

**Copy existing data from shared DB to new databases**:
```sql
-- Run for each service database
INSERT INTO RetailMonolith_Products.dbo.Products
SELECT * FROM RetailMonolith.dbo.Products;

INSERT INTO RetailMonolith_Orders.dbo.Orders
SELECT * FROM RetailMonolith.dbo.Orders;

INSERT INTO RetailMonolith_Orders.dbo.OrderLines
SELECT * FROM RetailMonolith.dbo.OrderLines;

-- Verify row counts match
SELECT COUNT(*) FROM RetailMonolith.dbo.Products;
SELECT COUNT(*) FROM RetailMonolith_Products.dbo.Products;
```

#### Step 4: Switch Reads

**Update services to READ from new database, WRITE to both**:
```csharp
// Read from new DB
var products = await newDb.Products.ToListAsync();

// Still write to both (for rollback safety)
newDb.Products.Add(product);
await newDb.SaveChangesAsync();
oldDb.Products.Add(product);
await oldDb.SaveChangesAsync();
```

**Test extensively**: Compare query results from old vs new DB

**Duration**: 3-5 days (monitor for discrepancies)

#### Step 5: Stop Dual Writes

**Remove writes to old database**:
```csharp
// Only write to new DB
newDb.Products.Add(product);
await newDb.SaveChangesAsync();
```

**Mark migration complete**: Tag in Git, document in ADR

#### Step 6: Archive Old Tables

**Drop tables from shared database** (after 1-2 week safety period):
```sql
-- Backup first!
BACKUP DATABASE RetailMonolith TO DISK = 'C:\Backups\RetailMonolith_PreCleanup.bak';

-- Drop tables (services no longer use these)
DROP TABLE RetailMonolith.dbo.OrderLines;
DROP TABLE RetailMonolith.dbo.Orders;
DROP TABLE RetailMonolith.dbo.Products;
-- etc.
```

### Data Consistency Patterns

#### Pattern 1: Denormalization

**Problem**: OrderLine references Product, but they're in different databases

**Solution**: Capture product details at order creation time (snapshot)
```csharp
// When creating order, copy Product.Name, Product.Price into OrderLine
// OrderLine is immutable - if Product changes, existing orders unchanged
```

#### Pattern 2: API Composition

**Problem**: Display order with current product details (e.g., updated product image)

**Solution**: Frontend calls both Order Service and Product Service, merges in UI
```javascript
// Frontend pseudo-code
const order = await fetch('/api/orders/1');
const productDetails = await Promise.all(
    order.lines.map(line => fetch(`/api/products/by-sku/${line.sku}`))
);

const enrichedOrder = {
    ...order,
    lines: order.lines.map((line, i) => ({
        ...line,
        currentProductImage: productDetails[i].imageUrl  // Latest image
    }))
};
```

#### Pattern 3: Event-Driven Sync (Optional, Phase 5)

**Problem**: Need to invalidate caches when product changes

**Solution**: Publish `ProductUpdated` event, subscribers update their caches
```csharp
// Product Service
app.MapPut("/api/products/{id}", async (int id, Product product, IEventBus eventBus) =>
{
    // Update database
    await db.SaveChangesAsync();
    
    // Publish event
    await eventBus.PublishAsync(new ProductUpdatedEvent(product.Id, product.Sku));
});

// Cart Service subscribes
eventBus.Subscribe<ProductUpdatedEvent>(async (evt) =>
{
    // Invalidate cached product data
    await cache.RemoveAsync($"product:{evt.Sku}");
});
```

### Success Criteria

- ✅ Each service uses dedicated database
- ✅ No queries to old shared database
- ✅ Data verified consistent between old and new DBs
- ✅ Rollback plan tested and documented
- ✅ No data loss during migration

### Rollback Plan

**Scenario**: Data inconsistency discovered after switch

**Rollback Steps**:
1. **Immediate**: Switch reads back to old database (configuration change)
2. **Short-term**: Resume dual writes to both databases
3. **Investigation**: Compare data between old and new DBs, identify discrepancies
4. **Resolution**: 
   - If new DB corrupt → discard, restore from old DB
   - If old DB corrupt → backfill from new DB
5. **Re-attempt**: Fix migration script, try again

**Rollback Time**: 1-4 hours (depending on data volume)

**Critical**: Maintain frequent backups during migration period

---

## Phase 5: Frontend Modernisation (Optional)

**Goal**: Decouple frontend from backend services

**Duration**: 2-4 weeks  
**Risk**: Medium (UI changes, user experience impact)  
**Rollback Complexity**: Medium

### Options

#### Option 1: Continue Razor Pages (BFF Pattern)

**Keep server-side rendering, call services via HTTP**

**Pros**: 
- No JavaScript framework required
- Server-side rendering (SEO friendly)
- Minimal changes to existing UI

**Cons**:
- Tighter coupling between frontend and backend
- Less interactive UX

#### Option 2: SPA (React/Vue/Angular)

**Build Single Page Application, call services via REST APIs**

**Pros**:
- Modern UX (responsive, interactive)
- Clear separation frontend/backend
- Can deploy SPA independently

**Cons**:
- New technology stack (learning curve)
- SEO challenges (requires SSR or pre-rendering)
- More complex authentication (JWT tokens)

#### Option 3: Hybrid (Razor Pages + HTMX/Alpine.js)

**Enhance Razor Pages with lightweight JavaScript for interactivity**

**Pros**:
- Progressive enhancement
- Keep server-side rendering
- Add interactivity where needed

**Cons**:
- Still coupled to backend
- Less suitable for mobile apps

### Recommendation

**Start with Option 1** (Razor Pages BFF) for Phases 1-4, then evaluate Option 2/3 based on:
- Team JavaScript expertise
- Mobile app requirements
- User feedback on current UI

---

## Risk Management

### Risk Register

| Risk | Mitigation | Contingency |
|------|-----------|-------------|
| **Service communication failures** | Circuit breakers, retries, timeouts | Fallback to monolith, feature flags |
| **Data inconsistency** | Dual writes, extensive testing | Manual reconciliation scripts |
| **Performance degradation** | Load testing, caching, monitoring | Scale horizontally, add caching layer |
| **Team skill gaps** | Training, pair programming | Hire consultant, slow down pace |
| **Scope creep** | Strict "no new features" rule | Defer enhancements to post-migration |

### Go/No-Go Criteria per Phase

**Before proceeding to next phase, verify**:
- ✅ All success criteria met
- ✅ No critical bugs in current phase
- ✅ Rollback plan tested successfully
- ✅ Stakeholder sign-off obtained
- ✅ Team confident in next phase estimate

**If any criterion fails → STOP, address issues before continuing**

---

## Timeline and Milestones

```
Week 1-2:   Phase 0 - Foundation & Containerisation
            Milestone: Monolith runs in Docker, CI/CD operational
            
Week 3-4:   Phase 1 - Extract Order Service
            Milestone: Order Service deployed, monolith calls via HTTP
            
Week 5-6:   Phase 2 - Extract Product Service
            Milestone: Product Service with caching, frontend calls both services
            
Week 7-9:   Phase 3 - Extract Inventory & Checkout Services
            Milestone: Saga orchestration working, distributed transactions tested
            
Week 10-12: Phase 4 - Database Decomposition
            Milestone: Each service has dedicated database, old DB archived
            
Week 13+:   Phase 5 - Frontend Modernisation (Optional)
            Milestone: SPA or enhanced Razor Pages deployed
```

**Total Duration**: 8-12 weeks (depending on team size and velocity)

---

## Success Metrics

### Technical Metrics

| Metric | Target | Measurement |
|--------|--------|-------------|
| **Service independence** | 100% services deployable independently | Deployment pipeline |
| **Response time** | p95 < 500ms per service | Application Insights |
| **Error rate** | < 0.1% per endpoint | Log aggregation |
| **Deployment frequency** | 2+ per week per service | CI/CD metrics |
| **Mean time to recovery (MTTR)** | < 30 minutes | Incident tracking |

### Business Metrics

| Metric | Target | Measurement |
|--------|--------|-------------|
| **Downtime during migration** | 0 minutes | Uptime monitoring |
| **Functionality changes** | 0 (behaviour preservation) | Regression testing |
| **Time to market (post-migration)** | -50% (faster deployments) | Velocity tracking |

---

## Conclusion

This migration plan provides a safe, incremental path from monolith to microservices. Each phase:
- ✅ Delivers value independently
- ✅ Can be rolled back if issues arise
- ✅ Builds on previous phases
- ✅ Reduces risk through gradual extraction

**First Slice (Phase 1)** is deliberately minimal:
- Extract read-only Order Service
- Minimal dependencies
- Low risk
- Demonstrates pattern for future phases

**Key Success Factors**:
1. Strict adherence to behaviour preservation
2. Comprehensive testing at each phase
3. Rollback plan tested before forward migration
4. Team buy-in and skill development
5. Stakeholder communication and expectation management

**Next Steps**:
1. Review and approve this migration plan
2. Create ADRs for architectural decisions
3. Set up Phase 0 infrastructure
4. Begin Phase 1 extraction

---

## References

- [Martin Fowler - Strangler Fig Application](https://martinfowler.com/bliki/StranglerFigApplication.html)
- [Sam Newman - Monolith to Microservices (Chapter 3: Splitting the Database)](https://www.oreilly.com/library/view/monolith-to-microservices/9781492047834/)
- [Chris Richardson - Microservices Patterns (Saga Pattern)](https://microservices.io/patterns/data/saga.html)
- [Microsoft - Data Considerations for Microservices](https://docs.microsoft.com/en-us/azure/architecture/microservices/design/data-considerations)
