# Low-Level Design (LLD) — Retail Monolith

## Key Classes and Services by Domain

### Products Domain

#### Models/Product.cs
```
Product (entity)
├── Id: int (PK)
├── Sku: string (unique index)
├── Name: string
├── Description: string?
├── Price: decimal
├── Currency: string
├── IsActive: bool
└── Category: string?
```

#### Pages/Products/Index.cshtml.cs::IndexModel
- **Dependencies**: `AppDbContext`, `ICartService`
- **Responsibilities**:
  - Load active products for display (`OnGetAsync`)
  - Map category names to Unsplash image URLs (`GetImageForCategory`)
  - Handle "Add to Cart" form post (`OnPostAsync`)
- **Key Methods**:
  - `OnGetAsync()`: Retrieves all active products from database (line 45)
  - `OnPostAsync(int productId)`: Adds product to cart and redirects to cart page (lines 47-79)

**Notable Implementation Detail**: `OnPostAsync` contains duplicate cart retrieval logic (lines 53-68) that should be delegated to `CartService`, but instead directly manipulates the database. This creates coupling between the UI layer and data layer.

---

### Inventory Domain

#### Models/InventoryItem.cs
```
InventoryItem (entity)
├── Id: int (PK)
├── Sku: string (unique index)
└── Quantity: int
```

**Access Pattern**: Inventory is not exposed via any UI or API endpoint. It is only accessed internally by `CheckoutService` during the checkout process.

**Coupling**: Inventory is coupled to Products via the `Sku` field (not a foreign key, string-based join).

---

### Cart Domain

#### Models/Cart.cs
```
Cart (aggregate root)
├── Id: int (PK)
├── CustomerId: string (default: "guest")
└── Lines: List<CartLine>

CartLine (entity)
├── Id: int (PK)
├── CartId: int (FK → Carts.Id)
├── Cart: Cart? (navigation)
├── Sku: string
├── Name: string
├── UnitPrice: decimal
└── Quantity: int
```

#### Services/ICartService.cs & Services/CartService.cs
- **Scope**: Scoped (per HTTP request)
- **Dependencies**: `AppDbContext`
- **Responsibilities**: CRUD operations for shopping carts

**Key Methods**:

1. **`GetOrCreateCartAsync(string customerId, CancellationToken ct)`** (lines 68-85)
   - Retrieves existing cart or creates new one
   - Returns: `Cart` with lines included
   - Side effect: Persists new cart to database if not found

2. **`AddToCartAsync(string customerId, int productId, int quantity, CancellationToken ct)`** (lines 12-47)
   - Validates product exists
   - Updates quantity if SKU already in cart
   - Adds new line if SKU not in cart
   - Persists changes

3. **`GetCartWithLinesAsync(string customerId, CancellationToken ct)`** (lines 60-66)
   - Returns cart with lines or empty cart instance (never null)
   - Does not persist empty cart

4. **`ClearCartAsync(string customerId, CancellationToken ct)`** (lines 49-58)
   - Deletes cart and all associated lines
   - No-op if cart not found

#### Pages/Cart/Index.cshtml.cs::IndexModel
- **Dependencies**: `ICartService`
- **Responsibilities**: Display cart contents
- **Key Method**:
  - `OnGetAsync()`: Loads cart lines into in-memory tuple list for display (lines 30-36)

**Design Note**: Page model uses `List<(string Name, int Quantity, decimal Price)>` instead of domain model, creating a view-specific projection.

---

### Checkout Domain

#### Services/ICheckoutService.cs & Services/CheckoutService .cs
- **Scope**: Scoped (per HTTP request)
- **Dependencies**: `AppDbContext`, `IPaymentGateway`
- **Responsibilities**: Orchestrate checkout process (cart → payment → order)

**Key Method**: `CheckoutAsync(string customerId, string paymentToken, CancellationToken ct)`

**Implementation Flow** (lines 16-56):

```
1. Pull cart from database (lines 19-22)
   └─> Throws if cart not found

2. Calculate total (line 24)
   └─> Sum(line.UnitPrice * line.Quantity)

3. Reserve inventory (lines 27-32)
   └─> For each line:
       ├─> Lookup InventoryItem by SKU
       ├─> Throw if insufficient stock
       └─> Decrement inventory quantity

4. Charge payment (lines 35-36)
   └─> Call IPaymentGateway.ChargeAsync
   └─> Set order status: "Paid" or "Failed"

5. Create order (lines 38-46)
   └─> Map CartLine → OrderLine
   └─> Persist Order entity

6. Clear cart (lines 51-52)
   └─> Delete all CartLines
   └─> Commit transaction

7. Return order (line 55)
```

**Critical Flaw**: Inventory is decremented (line 31) *before* payment is charged (line 35). If payment fails, inventory is still reduced, leading to stock loss.

**Transaction Boundary**: Entire checkout is a single database transaction (implicit via SaveChangesAsync at line 52).

#### Services/IPaymentGateway.cs & Services/MockPaymentGateway.cs
- **Abstraction**: Interface with two record types
  - `PaymentRequest(decimal Amount, string Currency, string Token)`
  - `PaymentResult(bool Succeeded, string? ProviderRef, string? Error)`

- **Mock Implementation**: Always returns success (line 9)
  - `Succeeded = true`
  - `ProviderRef = "MOCK-{Guid}"`
  - `Error = null`

#### Pages/Checkout/Index.cshtml.cs::IndexModel
- **Dependencies**: `ICartService`, `ICheckoutService`
- **Responsibilities**: Display checkout summary and process checkout
- **Key Methods**:
  - `OnGetAsync()`: Load cart lines for display (lines 29-35)
  - `OnPostAsync()`: Invoke `CheckoutService.CheckoutAsync` and redirect to order details (lines 37-50)

**Minimal API Alternative**: `POST /api/checkout` (Program.cs:51-55)
- Hardcoded customer ID: `"guest"`
- Hardcoded payment token: `"tok_test"`
- Returns JSON: `{ Id, Status, Total }`

---

### Orders Domain

#### Models/Order.cs
```
Order (aggregate root)
├── Id: int (PK)
├── CreatedUtc: DateTime (default: UtcNow)
├── CustomerId: string (default: "guest")
├── Status: string (default: "Created")
├── Total: decimal
└── Lines: List<OrderLine>

OrderLine (entity)
├── Id: int (PK)
├── OrderId: int (FK → Orders.Id)
├── Order: Order? (navigation)
├── Sku: string
├── Name: string
├── UnitPrice: decimal
└── Quantity: int
```

**Status Values**: `"Created"`, `"Paid"`, `"Failed"`, `"Shipped"` (comment on line 8)

#### Pages/Orders/Index.cshtml.cs::IndexModel
- **Dependencies**: `AppDbContext`
- **Responsibilities**: Display all orders (no filtering)
- **Key Method**:
  - `OnGetAsync()`: Load all orders with lines, ordered by CreatedUtc descending (lines 16-22)

**Coupling Hotspot**: Direct database access via `AppDbContext` instead of service layer.

#### Pages/Orders/Details.cshtml.cs::DetailsModel
- **Dependencies**: `AppDbContext`
- **Responsibilities**: Display single order details
- **Key Method**:
  - `OnGetAsync(int id)`: Load order by ID with lines (lines 15-20)

**Coupling Hotspot**: Direct database access via `AppDbContext` instead of service layer.

**Minimal API Alternative**: `GET /api/orders/{id}` (Program.cs:57-63)
- Direct `AppDbContext` injection into lambda
- Returns 404 if order not found, else returns entire Order entity as JSON

---

## Data Access Layer

### Data/AppDbContext.cs
- **Inherits**: `DbContext`
- **DbSets** (lines 13-19):
  - `Products`
  - `Inventory`
  - `Carts`
  - `CartLines`
  - `Orders`
  - `OrderLines`

**Schema Configuration** (lines 24-28):
- Unique index on `Product.Sku`
- Unique index on `InventoryItem.Sku`

**Seeding Logic** (`SeedAsync` method, lines 30-60):
- Checks if Products table is empty (line 32)
- Generates 50 products with:
  - SKU: `SKU-0001` to `SKU-0050`
  - Random category (Apparel, Footwear, Accessories, Electronics, Home, Beauty)
  - Random price: £5-£105
  - Currency: `"GBP"`
- Generates matching InventoryItems with random quantity (10-200)
- Persists in a single transaction

### Data/DesignTimeDbContextFactory.cs
- **Purpose**: Enables EF migrations tooling (`dotnet ef` commands)
- **Connection String**: Hardcoded LocalDB connection (line 17)

---

## Request Execution Flows

### Flow 1: Browse Products → Add to Cart
**User Journey**: User views products and adds one to cart

1. **Request**: `GET /Products`
   - Handler: `Pages/Products/Index.cshtml.cs::OnGetAsync` (line 45)
   - Query: `SELECT * FROM Products WHERE IsActive = 1`
   - Response: Razor page renders product grid

2. **Request**: `POST /Products` (form handler)
   - Handler: `Pages/Products/Index.cshtml.cs::OnPostAsync(productId)` (lines 47-79)
   - Steps:
     1. Lookup product by ID (line 50)
     2. Retrieve or create cart for "guest" (lines 53-68)
     3. Add CartLine with product details (lines 70-76)
     4. Call `CartService.AddToCartAsync("guest", productId)` (line 77)
     5. Redirect to `/Cart` (line 78)

**Redundancy**: Steps 2-4 duplicate logic already in `CartService`, creating unnecessary complexity.

---

### Flow 2: View Cart → Checkout → Order Confirmation
**User Journey**: User reviews cart, checks out, and views order

1. **Request**: `GET /Cart`
   - Handler: `Pages/Cart/Index.cshtml.cs::OnGetAsync` (lines 30-36)
   - Service Call: `CartService.GetCartWithLinesAsync("guest")`
   - Query: `SELECT * FROM Carts WHERE CustomerId = 'guest' INCLUDE CartLines`
   - Response: Razor page renders cart lines and total

2. **Request**: `GET /Checkout`
   - Handler: `Pages/Checkout/Index.cshtml.cs::OnGetAsync` (lines 29-35)
   - Service Call: `CartService.GetCartWithLinesAsync("guest")`
   - Query: Same as cart retrieval
   - Response: Razor page renders checkout form with cart summary

3. **Request**: `POST /Checkout` (form submission)
   - Handler: `Pages/Checkout/Index.cshtml.cs::OnPostAsync` (lines 37-50)
   - Service Call: `CheckoutService.CheckoutAsync("guest", PaymentToken)`
   - Steps (see CheckoutService flow above):
     1. Load cart with lines
     2. Calculate total
     3. Decrement inventory (risky: before payment)
     4. Charge payment via `MockPaymentGateway`
     5. Create order entity
     6. Delete cart lines
     7. Commit transaction
   - Response: Redirect to `/Orders/Details?id={orderId}`

4. **Request**: `GET /Orders/Details?id={orderId}`
   - Handler: `Pages/Orders/Details.cshtml.cs::OnGetAsync(id)` (lines 15-20)
   - Query: `SELECT * FROM Orders WHERE Id = {id} INCLUDE OrderLines`
   - Response: Razor page renders order confirmation

---

### Flow 3: API Checkout (Headless)
**Alternative Flow**: Programmatic checkout via REST API

1. **Request**: `POST /api/checkout`
   - Handler: Minimal API lambda in `Program.cs` (lines 51-55)
   - Service Call: `CheckoutService.CheckoutAsync("guest", "tok_test")`
   - Response: JSON `{ "Id": 1, "Status": "Paid", "Total": 49.99 }`

2. **Request**: `GET /api/orders/1`
   - Handler: Minimal API lambda in `Program.cs` (lines 57-63)
   - Query: `SELECT * FROM Orders WHERE Id = 1 INCLUDE OrderLines`
   - Response: JSON representation of entire Order entity

---

## Coupling Hotspots

### 1. Pages → AppDbContext (High Risk)
**Location**: `Pages/Products/Index.cshtml.cs`, `Pages/Orders/Index.cshtml.cs`, `Pages/Orders/Details.cshtml.cs`

**Risk**: UI layer directly queries database, bypassing service layer. Changes to database schema or query logic require updates to both services and pages.

**Example**: `Pages/Orders/Index.cshtml.cs:18-21`
```csharp
Orders = await _db.Orders
    .Include(o => o.Lines)
    .OrderByDescending(o => o.CreatedUtc)
    .ToListAsync();
```

**Impact**: High coupling, difficult to test, violates separation of concerns.

---

### 2. Product.Sku ↔ InventoryItem.Sku (Medium Risk)
**Location**: `CheckoutService.cs:29`, `AppDbContext.cs:58`

**Risk**: String-based join between Products and Inventory. No referential integrity enforced by database. SKU mismatches will cause runtime exceptions.

**Example**: `CheckoutService.cs:29`
```csharp
var inv = await _db.Inventory.SingleAsync(i => i.Sku == line.Sku, ct);
```

**Impact**: Potential for orphaned inventory records, data integrity issues.

---

### 3. Checkout Flow Inventory Decrement (Critical Risk)
**Location**: `CheckoutService.cs:27-36`

**Risk**: Inventory is decremented *before* payment is charged. If payment fails, inventory is lost.

**Current Behavior**:
```csharp
// 2) reserve/decrement stock (optimistic)
foreach (var line in cart.Lines) {
    var inv = await _db.Inventory.SingleAsync(i => i.Sku == line.Sku, ct);
    if (inv.Quantity < line.Quantity) throw new InvalidOperationException($"Out of stock: {line.Sku}");
    inv.Quantity -= line.Quantity; // <-- Decremented here
}

// 3) charge
var pay = await _payments.ChargeAsync(new(total, "GBP", paymentToken), ct);
var status = pay.Succeeded ? "Paid" : "Failed"; // <-- Payment may fail here
```

**Expected Behavior**: Decrement inventory only *after* successful payment, or use compensating transactions to restore inventory on payment failure.

**Impact**: Stock loss on failed payments, inaccurate inventory tracking.

---

### 4. Hardcoded Customer ID (High Risk)
**Location**: Throughout application (e.g., `Program.cs:53`, `Pages/Cart/Index.cshtml.cs:32`, `CheckoutService.cs:16`)

**Risk**: All users share the same customer ID (`"guest"`). No user isolation. Production deployment would allow any user to see/modify any other user's cart and orders.

**Impact**: Security vulnerability, data privacy violation, cannot support multi-tenancy.

---

### 5. Auto-Migration on Startup (Critical Risk)
**Location**: `Program.cs:27`

**Risk**: Database migrations applied automatically on every application startup. In production, this can cause:
- Downtime during migration execution
- Data loss if migration is destructive
- Concurrent migration attempts in multi-instance deployments

**Current Code**:
```csharp
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.MigrateAsync(); // <-- Runs on every startup
    await AppDbContext.SeedAsync(db);
}
```

**Impact**: High risk of production outages, data corruption.

---

## Complexity Indicators

| Metric | Value | Assessment |
|--------|-------|------------|
| **Total Entity Classes** | 6 (Product, InventoryItem, Cart, CartLine, Order, OrderLine) | Low |
| **Total Service Classes** | 3 (CartService, CheckoutService, MockPaymentGateway) | Low |
| **Total Razor Pages** | 7 (Index, Products, Cart, Checkout, Orders, Orders/Details, Error) | Low |
| **Total Minimal APIs** | 2 (POST /api/checkout, GET /api/orders/{id}) | Low |
| **Database Migrations** | 1 (Initial) | Low |
| **External Dependencies** | 1 (MockPaymentGateway, simulated) | Low |
| **Direct DbContext Access** | 5 locations (Products, Orders pages, Minimal APIs) | **High (anti-pattern)** |
| **Coupling Hotspots** | 5 major issues identified | **High** |

---

## Testing Considerations

### Testable Components
- `CartService` (isolated, depends only on DbContext)
- `CheckoutService` (requires mocked `IPaymentGateway` and DbContext)

### Difficult to Test
- Razor Pages with direct `AppDbContext` dependencies (require database integration tests)
- Minimal API lambdas in `Program.cs` (require full application host)

### Missing Test Infrastructure
- No test projects found in solution
- No test data builders or fixtures
- No integration test harness

---

## Deployment Topology

**Current**: Single-process monolith
- All domains co-located in one ASP.NET Core process
- Shared database connection pool
- Shared application lifecycle (startup/shutdown)

**Scaling Limitations**:
- Cannot scale individual domains independently
- Single point of failure (entire app fails if any domain fails)
- Shared resource contention (CPU, memory, database connections)

---

## Summary

The Retail Monolith implements a functional e-commerce flow with clear domain separation at the logical level (Products, Inventory, Cart, Checkout, Orders). However, architectural coupling issues create risks:

1. **Service Layer Bypass**: Pages directly access `AppDbContext`, circumventing business logic encapsulation.
2. **Inventory-Payment Race Condition**: Critical flaw in `CheckoutService` causes stock loss on failed payments.
3. **No Authentication**: Hardcoded customer ID prevents multi-user scenarios.
4. **Unsafe Migration Strategy**: Auto-migration on startup is unsuitable for production.
5. **String-Based Joins**: SKU coupling between Products and Inventory lacks referential integrity.

These issues should be addressed before any modernization or microservices decomposition work begins.
