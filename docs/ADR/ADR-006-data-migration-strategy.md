# ADR-006: Data Migration Strategy - Shared Database to Database per Service

**Status**: Proposed  
**Date**: 2025-01-21  
**Context**: Modernisation Planning

## Context

The current monolith uses a single SQL Server database shared across all domains (Products, Inventory, Cart, Orders). As we extract services, we must decide:
- When to separate databases (immediate vs deferred)
- How to migrate data safely
- How to maintain consistency across databases
- How to handle cross-service queries

Database separation is a critical decision that impacts:
- Service independence (shared DB = shared fate)
- Deployment flexibility (schema changes require coordination)
- Scalability (cannot scale DB per service needs)
- Data consistency (distributed transactions vs eventual consistency)

## Decision

Adopt a **phased approach**: **Shared Database First (Phases 1-3), Database per Service Later (Phase 4+)**

### Phase 1-3: Shared Database

**All services connect to the same SQL Server database** (`RetailMonolith`)

**Each service uses its own DbContext** with subset of entities:
```csharp
// Product Service
public class ProductDbContext : DbContext
{
    public DbSet<Product> Products { get; set; }
    // Only Products table exposed
}

// Order Service  
public class OrderDbContext : DbContext
{
    public DbSet<Order> Orders { get; set; }
    public DbSet<OrderLine> OrderLines { get; set; }
    // Only Orders/OrderLines tables exposed
}
```

**Services responsible for their own data integrity** but schema remains shared.

### Phase 4+: Database per Service

**Separate databases created**:
- `RetailMonolith_Products` → Product Service
- `RetailMonolith_Orders` → Order Service
- `RetailMonolith_Cart` → Cart Service
- `RetailMonolith_Inventory` → Inventory Service

**Migration strategy: Dual Writes**

1. **Create new databases** with identical schema
2. **Dual write phase**: Services write to both old and new DBs (1 week)
3. **Backfill historical data** from old DB to new DB
4. **Switch reads** to new DB, continue dual writes (3-5 days)
5. **Verify consistency** between old and new DBs
6. **Stop dual writes**, remove old DB references
7. **Archive old tables** after 1-2 week safety period

### Cross-Database Consistency

**Pattern 1: Denormalization**
- Capture snapshots at transaction time (e.g., Product name/price in OrderLine)
- Acceptable for immutable data (orders)

**Pattern 2: API Composition**
- Frontend calls multiple services, merges results
- Acceptable for real-time queries (e.g., order with current product details)

**Pattern 3: Event-Driven Sync** (Phase 5+)
- Publish events when data changes (e.g., `ProductUpdated`)
- Subscribers update their denormalized copies
- Eventual consistency acceptable

## Rationale

### Why Shared Database First?

**Minimizes migration risk**:
- ✅ No data movement required
- ✅ Existing transactions still work (single DB)
- ✅ Easy rollback (just route traffic back)
- ✅ Faster to implement (less complexity)
- ✅ Can extract services without data migration concerns

**Buys time to validate service boundaries**:
- ✅ Test service interactions before committing to data separation
- ✅ Refactor service boundaries if needed (easier with shared DB)
- ✅ Identify missing data needs early

**Proven pattern**:
- ✅ Recommended by Sam Newman (Monolith to Microservices)
- ✅ Used successfully by Netflix, Amazon, others

### Why Database per Service Eventually?

**Achieves true service independence**:
- ✅ Services can be deployed without coordinating schema changes
- ✅ Each service can choose optimal DB technology (SQL, NoSQL, etc.)
- ✅ Database can be scaled per service needs (high read vs high write)

**Reduces coupling**:
- ✅ No shared schema = no accidental coupling via database joins
- ✅ Forces explicit service APIs (no bypassing via DB queries)

**Enables independent lifecycle**:
- ✅ Each service can upgrade DB version independently
- ✅ Each service can optimize schema without impacting others

### Why Not Database per Service from the Start?

**High risk**:
- ❌ Data migration errors could cause data loss
- ❌ Distributed transactions complex (saga pattern required immediately)
- ❌ Cross-service queries difficult (no joins across databases)

**Premature optimization**:
- ❌ Service boundaries may change during early phases
- ❌ Data relationships not fully understood yet
- ❌ May extract wrong services first, requiring re-migration

**Delays value delivery**:
- ❌ Longer time to first extracted service (more upfront work)
- ❌ Cannot validate service patterns until data migration complete

### Why Dual Writes?

**Safe, reversible migration**:
- ✅ Old DB remains source of truth during transition
- ✅ Can switch back to old DB if issues found
- ✅ Both DBs in sync during migration period

**Proven technique**:
- ✅ Used by LinkedIn, Uber, Shopify for large-scale migrations
- ✅ Well-documented pattern with known pitfalls

## Consequences

### Positive

**Phases 1-3 (Shared DB)**:
- ✅ Fast service extraction (no data migration overhead)
- ✅ Low risk rollback (just route traffic)
- ✅ Existing transactions work (single DB)
- ✅ No data synchronization issues

**Phase 4+ (Database per Service)**:
- ✅ True service independence
- ✅ Optimized schemas per service
- ✅ Independent scaling per service
- ✅ Technology flexibility (can use NoSQL if needed)

### Negative

**Phases 1-3 (Shared DB)**:
- ❌ Services not truly independent (shared DB = shared fate)
- ❌ Schema changes require coordination
- ❌ Cannot scale DB per service
- ❌ Risk of accidental coupling via DB queries

**Phase 4+ (Database per Service)**:
- ❌ Complex migration (dual writes, backfilling)
- ❌ Distributed transactions (saga pattern required)
- ❌ No cross-database joins (API composition needed)
- ❌ Data consistency challenges (eventual consistency)

### Risks

| Risk | Likelihood | Impact | Mitigation |
|------|-----------|--------|------------|
| **Data loss during migration** | Low | Critical | Extensive testing, backups, dual writes |
| **Data inconsistency between DBs** | Medium | High | Automated reconciliation scripts, monitoring |
| **Performance degradation (dual writes)** | Low | Medium | Async writes to new DB, batch updates |
| **Rollback complexity (Phase 4)** | Medium | High | Maintain dual writes for 1-2 weeks, frequent backups |
| **Service boundaries change** | Low | Medium | Keep dual writes active, defer final cutover |

## Alternatives Considered

### Alternative 1: Database per Service from Start

**Pros**: True service independence from day one  
**Cons**: High risk, complex migration, delayed value

**Rejected**: Too risky, violates incremental migration principle

### Alternative 2: Schema per Service (same DB instance)

**Pros**: Logical separation, easier migration than separate DBs  
**Cons**: Still shared DB instance (single point of failure), limited scaling

**Rejected**: Doesn't achieve true independence, only marginal benefit

### Alternative 3: Replicated Databases (read replicas)

**Pros**: Scale reads independently per service  
**Cons**: Still single write DB (bottleneck), replication lag (consistency issues)

**Rejected**: Doesn't solve write scaling or independence

### Alternative 4: Event Sourcing from Start

**Pros**: Natural event log for replication, audit trail  
**Cons**: High complexity, steep learning curve, overkill for simple CRUD

**Rejected**: Too complex for this application's needs

## Implementation Details

### Phase 1-3: Shared Database Setup

**Single connection string for all services**:
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=sqlserver;Database=RetailMonolith;User=sa;Password=***"
  }
}
```

**DbContext per service restricts access**:
```csharp
// Product Service - only sees Products
public class ProductDbContext : DbContext
{
    public DbSet<Product> Products { get; set; }
    
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Only configure Product table
        modelBuilder.Entity<Product>().ToTable("Products");
    }
}
```

**Migrations disabled in services** (monolith owns schema during Phases 1-3):
```csharp
// Do NOT call Database.Migrate() in services
// Monolith owns migrations until Phase 4
```

### Phase 4: Dual Write Implementation

**Dual write in Product Service**:
```csharp
public async Task CreateProductAsync(Product product)
{
    try
    {
        // Write to new DB (primary)
        _newDb.Products.Add(product);
        await _newDb.SaveChangesAsync();
        
        // Write to old DB (backup, for rollback safety)
        _oldDb.Products.Add(product);
        await _oldDb.SaveChangesAsync();
    }
    catch (Exception ex)
    {
        // If new DB write fails, don't write to old DB
        // If old DB write fails, log warning but continue (degraded mode)
        _logger.LogWarning(ex, "Failed to write to old DB (non-critical)");
    }
}
```

**Backfill script**:
```sql
-- Run once to copy historical data
USE RetailMonolith_Products;

INSERT INTO Products (Id, Sku, Name, Description, Price, Currency, IsActive, Category)
SELECT Id, Sku, Name, Description, Price, Currency, IsActive, Category
FROM RetailMonolith.dbo.Products;

-- Verify row counts match
SELECT COUNT(*) AS OldCount FROM RetailMonolith.dbo.Products;
SELECT COUNT(*) AS NewCount FROM RetailMonolith_Products.dbo.Products;
```

**Reconciliation script** (detect inconsistencies):
```sql
-- Find products in old DB but not new DB
SELECT * FROM RetailMonolith.dbo.Products old
WHERE NOT EXISTS (
    SELECT 1 FROM RetailMonolith_Products.dbo.Products new
    WHERE new.Id = old.Id
);

-- Find products with different data
SELECT old.*, new.*
FROM RetailMonolith.dbo.Products old
JOIN RetailMonolith_Products.dbo.Products new ON old.Id = new.Id
WHERE old.Name <> new.Name 
   OR old.Price <> new.Price
   OR old.IsActive <> new.IsActive;
```

### Cross-Database Queries

**Before (monolith, single DB)**:
```csharp
// Join Orders with Products
var orders = await _db.Orders
    .Include(o => o.Lines)
    .ThenInclude(l => l.Product)  // EF navigation property
    .ToListAsync();
```

**After (separate DBs, API composition)**:
```csharp
// Order Service - get orders
var orders = await _orderDb.Orders
    .Include(o => o.Lines)
    .ToListAsync();

// Call Product Service for each SKU
var productClient = _httpClientFactory.CreateClient("ProductService");
foreach (var order in orders)
{
    foreach (var line in order.Lines)
    {
        var product = await productClient.GetFromJsonAsync<Product>(
            $"/api/products/by-sku/{line.Sku}");
        
        // Merge into result
        line.CurrentProductDetails = product;
    }
}
```

**Optimization - batch API calls**:
```csharp
// Get all unique SKUs
var skus = orders.SelectMany(o => o.Lines.Select(l => l.Sku)).Distinct();

// Single batch API call
var products = await productClient.PostAsJsonAsync("/api/products/batch", skus)
    .Content.ReadFromJsonAsync<List<Product>>();

// Merge in-memory
var productBySku = products.ToDictionary(p => p.Sku);
foreach (var order in orders)
{
    foreach (var line in order.Lines)
    {
        line.CurrentProductDetails = productBySku[line.Sku];
    }
}
```

## Validation

### Success Criteria

**Phase 1-3 (Shared DB)**:
- ✅ All services connect to shared database successfully
- ✅ Each service only accesses its own tables (verified via logging)
- ✅ Schema changes deployed via monolith migrations (not services)

**Phase 4 (Dual Write)**:
- ✅ Services write to both old and new DBs (verified via row counts)
- ✅ Data consistency verified via reconciliation scripts
- ✅ No data loss detected (checksums match)

**Phase 4 (Database per Service)**:
- ✅ Services read from new DBs successfully
- ✅ Old DB writes stopped, tables archived
- ✅ Cross-service queries work via API composition
- ✅ Performance acceptable (response time < target)

### Testing Strategy

**Phase 1-3**:
- Integration tests verify each service accesses correct tables
- Load tests verify shared DB handles combined load
- Chaos tests verify service failures don't cascade

**Phase 4**:
- Dual write tests verify data written to both DBs
- Reconciliation tests run hourly to detect inconsistencies
- Rollback tests verify switching back to old DB works

## References

- [Sam Newman - Splitting the Monolith Database](https://samnewman.io/blog/2020/04/27/splitting-the-database/)
- [Martin Kleppmann - Designing Data-Intensive Applications (Chapter 7)](https://dataintensive.net/)
- [Chris Richardson - Decompose by Subdomain](https://microservices.io/patterns/decomposition/decompose-by-subdomain.html)
- [Uber - Schemaless: Uber Engineering's Scalable Datastore](https://eng.uber.com/schemaless-part-one/)

## Related ADRs

- ADR-005: Service Decomposition Strategy
- ADR-008: Saga Pattern for Distributed Transactions
