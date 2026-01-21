# Runbook — Retail Monolith

## System Overview

The Retail Monolith is an ASP.NET Core 9 Razor Pages application with SQL Server LocalDB for development. This runbook provides exact commands for building, running, testing, and troubleshooting the application.

---

## Prerequisites

### Required Software
- **.NET 9 SDK** (current version: 10.0.101 detected in environment)
  - Download: https://dotnet.microsoft.com/download/dotnet/9.0
  - Verify: `dotnet --version`

- **SQL Server LocalDB** (for Windows) or **SQL Server** (for Linux/macOS)
  - LocalDB included with Visual Studio
  - Standalone installer: https://learn.microsoft.com/sql/database-engine/configure-windows/sql-server-express-localdb
  - Verify LocalDB: `SqlLocalDB.exe info`
  - For non-Windows: Update connection string in `appsettings.Development.json` to point to SQL Server instance

### Optional Tools
- **Visual Studio 2022** or **VS Code** with C# extension
- **Git** (for version control)
- **Docker Desktop** (for Dev Container option)

---

## Build Commands

### Restore Dependencies
```bash
cd /home/runner/work/ads_monolith_skills_agents/ads_monolith_skills_agents
dotnet restore
```

**Expected Output**:
```
Restore completed in [X]ms for RetailMonolith.csproj.
```

**Packages Restored** (from `RetailMonolith.csproj`):
- Microsoft.EntityFrameworkCore.SqlServer (9.0.9)
- Microsoft.EntityFrameworkCore.Design (9.0.9)
- Microsoft.AspNetCore.Diagnostics.HealthChecks (2.2.0)
- Microsoft.Extensions.Http.Polly (9.0.9)

### Build Application
```bash
dotnet build
```

**Expected Output**:
```
Build succeeded.
    0 Warning(s)
    0 Error(s)
```

**Artifacts**: `bin/Debug/net9.0/`

### Clean Build (Remove Artifacts)
```bash
dotnet clean
```

---

## Database Setup

### Apply Migrations (Manual)
```bash
dotnet ef database update
```

**What This Does**:
- Applies all migrations from `Migrations/` folder
- Creates database if not exists: `RetailMonolith`
- Creates tables: `Products`, `Inventory`, `Carts`, `CartLines`, `Orders`, `OrderLines`

**Expected Output**:
```
Applying migration '20251019185248_Initial'.
Done.
```

**Connection String** (from `Data/DesignTimeDbContextFactory.cs`):
```
Server=(localdb)\MSSQLLocalDB;Database=RetailMonolith;Trusted_Connection=True;MultipleActiveResultSets=true
```

### Reset Database (Drop and Recreate)
```bash
dotnet ef database drop -f
dotnet ef database update
```

**Use Case**: Clear all data and start fresh (useful for testing seeding logic).

### Create New Migration (After Model Changes)
```bash
dotnet ef migrations add <MigrationName>
```

**Example**:
```bash
dotnet ef migrations add AddProductImageUrl
```

**What This Does**:
- Generates migration files in `Migrations/` folder
- Does NOT apply migration (must run `dotnet ef database update`)

---

## Run Commands

### Run Application (Development Mode)
```bash
dotnet run
```

**What Happens on Startup** (from `Program.cs:24-29`):
1. Automatic migration: `await db.Database.MigrateAsync();`
2. Automatic seeding: `await AppDbContext.SeedAsync(db);` (inserts 50 products if database empty)
3. Web server starts

**Expected Output**:
```
Building...
info: Microsoft.Hosting.Lifetime[14]
      Now listening on: https://localhost:5001
info: Microsoft.Hosting.Lifetime[14]
      Now listening on: http://localhost:5000
```

**Access Application**:
- HTTPS: https://localhost:5001
- HTTP: http://localhost:5000

### Run with Specific Environment
```bash
ASPNETCORE_ENVIRONMENT=Production dotnet run
```

**Configuration Precedence**:
1. `appsettings.json` (base)
2. `appsettings.{Environment}.json` (override)
3. Environment variables (highest priority)

### Run with Custom Connection String
```bash
ConnectionStrings__DefaultConnection="Server=myserver;Database=RetailMonolith;..." dotnet run
```

### Stop Application
- **Interactive mode**: Press `Ctrl+C`
- **Background process**: `kill <PID>`

---

## Test Commands

### Run Tests
```bash
dotnet test
```

**Current Status**: No test projects found in solution (`RetailMonolith.sln`).

**Expected Output**:
```
No test projects found.
```

**To Add Tests**:
1. Create test project: `dotnet new xunit -n RetailMonolith.Tests`
2. Add reference: `dotnet add RetailMonolith.Tests reference RetailMonolith.csproj`
3. Add to solution: `dotnet sln add RetailMonolith.Tests/RetailMonolith.Tests.csproj`

---

## Available Endpoints

| Method | Path | Description | Handler |
|--------|------|-------------|---------|
| GET | `/` | Home page | `Pages/Index.cshtml` |
| GET | `/Products` | Product listing | `Pages/Products/Index.cshtml` |
| POST | `/Products` | Add to cart (form post) | `Pages/Products/Index.cshtml.cs::OnPostAsync` |
| GET | `/Cart` | View cart | `Pages/Cart/Index.cshtml` |
| GET | `/Checkout` | Checkout form | `Pages/Checkout/Index.cshtml` |
| POST | `/Checkout` | Submit checkout | `Pages/Checkout/Index.cshtml.cs::OnPostAsync` |
| GET | `/Orders` | Order history | `Pages/Orders/Index.cshtml` |
| GET | `/Orders/Details?id={id}` | Order details | `Pages/Orders/Details.cshtml` |
| POST | `/api/checkout` | Headless checkout API | `Program.cs:51-55` |
| GET | `/api/orders/{id}` | Get order by ID (JSON) | `Program.cs:57-63` |
| GET | `/health` | Health check (not mapped) | Configured but not exposed |

### Test Endpoints with cURL

**List Products** (view page):
```bash
curl -k https://localhost:5001/Products
```

**Checkout via API**:
```bash
curl -X POST https://localhost:5001/api/checkout \
  -H "Content-Type: application/json" \
  -k
```

**Expected Response**:
```json
{
  "id": 1,
  "status": "Paid",
  "total": 49.99
}
```

**Get Order Details**:
```bash
curl -k https://localhost:5001/api/orders/1
```

**Expected Response**: Full Order JSON with lines.

---

## Configuration Files

### appsettings.json (Base Configuration)
**Location**: `/home/runner/work/ads_monolith_skills_agents/ads_monolith_skills_agents/appsettings.json`

**Contents**:
```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AllowedHosts": "*"
}
```

### appsettings.Development.json (Development Overrides)
**Location**: `/home/runner/work/ads_monolith_skills_agents/ads_monolith_skills_agents/appsettings.Development.json`

**Contents**:
```json
{
  "DetailedErrors": true,
  "ConnectionStrings": {
    "DefaultConnection": "Server=(localdb)\\MSSQLLocalDB;Database=RetailMonolith;Trusted_Connection=True;MultipleActiveResultSets=true"
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  }
}
```

**Key Settings**:
- `DetailedErrors: true` — Shows detailed exception pages in development
- `ConnectionStrings.DefaultConnection` — Database connection string

### Environment Variables

Override configuration with environment variables using double underscore notation:

```bash
export ConnectionStrings__DefaultConnection="Server=myserver;Database=RetailMonolith;..."
export Logging__LogLevel__Default="Debug"
export ASPNETCORE_ENVIRONMENT="Staging"
```

---

## Local Dependencies

### SQL Server LocalDB
- **Purpose**: Development database engine
- **Database Name**: `RetailMonolith`
- **Instance Name**: `MSSQLLocalDB`
- **Auto-created**: Yes (on first migration)

**Verify LocalDB**:
```bash
SqlLocalDB.exe info
```

**Expected Output**:
```
MSSQLLocalDB
```

**Connect to LocalDB** (via SSMS or Azure Data Studio):
```
Server: (localdb)\MSSQLLocalDB
Database: RetailMonolith
Authentication: Windows Authentication
```

---

## Troubleshooting

### Issue: "LocalDB is not installed"
**Symptoms**:
```
A network-related or instance-specific error occurred while establishing a connection to SQL Server.
```

**Solution**:
1. Install SQL Server Express LocalDB: https://learn.microsoft.com/sql/database-engine/configure-windows/sql-server-express-localdb
2. Or update connection string to use SQL Server instance

---

### Issue: "Migration failed"
**Symptoms**:
```
An error occurred using the connection to database 'RetailMonolith' on server '(localdb)\MSSQLLocalDB'.
```

**Solution**:
1. Drop database: `dotnet ef database drop -f`
2. Recreate: `dotnet ef database update`
3. If still failing, check LocalDB is running: `SqlLocalDB.exe info MSSQLLocalDB`

---

### Issue: "Port already in use"
**Symptoms**:
```
Failed to bind to address https://127.0.0.1:5001: address already in use.
```

**Solution**:
1. Stop other instance: Find process using `lsof -i :5001` (macOS/Linux) or `netstat -ano | findstr :5001` (Windows)
2. Kill process: `kill <PID>`
3. Or change port in `Properties/launchSettings.json`

---

### Issue: "Products page is empty"
**Symptoms**: `/Products` page shows no products.

**Root Cause**: Seeding did not run (database already had products, or seeding logic skipped).

**Solution**:
```bash
dotnet ef database drop -f
dotnet ef database update
dotnet run
```

**Why This Works**: `AppDbContext.SeedAsync` only seeds if `Products` table is empty (line 32).

---

### Issue: "Checkout creates order but payment fails"
**Symptoms**: Order created with `Status = "Failed"`, inventory decremented.

**Root Cause**: `MockPaymentGateway` always succeeds. However, if you modify it to fail, inventory is already decremented (CheckoutService.cs:27-32).

**Workaround**: Do not modify `MockPaymentGateway`. This is a known bug documented in `docs/LLD.md` under "Coupling Hotspots".

---

### Issue: "Cannot see orders from other users"
**Symptoms**: All users see the same orders.

**Expected Behavior**: This is correct. All users use hardcoded customer ID `"guest"` (see `docs/ADR/ADR-003-hardcoded-guest-customer.md`).

**Solution**: This is a known limitation. To support multiple users, implement authentication and replace hardcoded `"guest"` with `User.Identity.Name` or similar.

---

## Known Limitations and Technical Debt

### Auto-Migration on Startup
**Risk**: Database migrations run automatically on every startup (`Program.cs:27`).

**Impact**:
- Safe for local development
- **UNSAFE for production** (can cause downtime, data loss, or race conditions)

**Recommendation**: Disable auto-migration in production. Add environment check:
```csharp
if (app.Environment.IsDevelopment())
{
    await db.Database.MigrateAsync();
    await AppDbContext.SeedAsync(db);
}
```

---

### Hardcoded Customer ID
**Risk**: All users share customer ID `"guest"` (no authentication).

**Impact**:
- Cannot deploy to production (no user isolation)
- All users see same cart and orders

**Recommendation**: Implement ASP.NET Core Identity or session-based customer IDs before production deployment.

---

### Inventory Decrement Before Payment
**Risk**: Inventory is decremented before payment is charged (`CheckoutService.cs:27-36`).

**Impact**: Failed payments result in lost inventory.

**Recommendation**: Move inventory decrement to after successful payment, or implement compensating transactions.

---

### No Tests
**Risk**: No automated tests exist.

**Impact**: Code changes cannot be validated automatically.

**Recommendation**: Add unit tests for `CartService` and `CheckoutService`, integration tests for Razor Pages.

---

### Mock Payment Gateway
**Risk**: `MockPaymentGateway` always succeeds (not production-ready).

**Impact**: Cannot test payment failure scenarios, no real money processing.

**Recommendation**: Integrate with Stripe.net or similar payment processor for production.

---

## Deployment Checklist

Before deploying to production, address these issues:

- [ ] **Disable auto-migration** on startup (use separate migration step in CI/CD)
- [ ] **Implement authentication** (replace hardcoded "guest" customer ID)
- [ ] **Replace MockPaymentGateway** with real payment processor (Stripe, PayPal, etc.)
- [ ] **Fix inventory-payment race condition** (decrement inventory after payment success)
- [ ] **Add connection string to Azure Key Vault** (remove from appsettings.json)
- [ ] **Enable HTTPS** with valid certificate (not self-signed)
- [ ] **Add logging and monitoring** (Application Insights, Serilog, etc.)
- [ ] **Add health check endpoint** (expose `/health` in routing)
- [ ] **Add automated tests** (unit, integration, E2E)
- [ ] **Review security** (SQL injection, XSS, CSRF protection)

---

## Contact and Escalation

**Documentation Owner**: System Discovery Agent  
**Documentation Date**: 2025-01-21  
**Source Code**: `/home/runner/work/ads_monolith_skills_agents/ads_monolith_skills_agents`  
**Related Docs**:
- [High-Level Design](./HLD.md)
- [Low-Level Design](./LLD.md)
- [Architecture Decision Records](./ADR/)

For issues or questions, refer to the README.md or create a GitHub issue.
