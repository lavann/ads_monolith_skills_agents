# Test Strategy — Retail Monolith

**Status**: Active  
**Date**: 2025-01-21  
**Purpose**: Establish a safety net for confident, incremental modernisation

## Executive Summary

This test strategy establishes automated testing and continuous integration for the Retail Monolith application. The primary goal is to **protect existing behavior** during the upcoming microservices migration, ensuring that no regressions are introduced as services are extracted.

**Key Principles**:
- Tests must pass against the existing monolith without functional refactoring
- Focus on integration and service-level tests over UI tests
- Test critical user flows end-to-end
- Enable fast feedback through CI automation

---

## Test Pyramid Approach

We adopt a pragmatic test pyramid optimized for monolith modernization:

```
        /\
       /  \     E2E Tests (Minimal)
      /____\    - Full application integration via HTTP
     /      \   
    /  INTE  \  Integration Tests (Primary Focus)
   /  GRATION \  - Service layer with real database
  /____________\ - Critical domain flows
 /              \
/  UNIT  TESTS   \ Unit Tests (Supporting)
\________________/ - Core business logic
                   - Calculation & validation
```

### Layer Distribution

| Layer | Coverage Target | Purpose | Trade-offs |
|-------|----------------|---------|------------|
| **Unit Tests** | ~40% | Fast feedback on business logic | Isolated from DB, may miss integration issues |
| **Integration Tests** | ~50% | Validate service layer with database | Slower but higher confidence |
| **E2E/API Tests** | ~10% | Verify critical user journeys | Slowest but catches contract issues |

---

## Critical Flows Covered

The following flows are **explicitly tested** to protect against regressions:

### ✅ Flow 1: Product Catalog → Add to Cart
**Business Value**: Core revenue-generating flow  
**Test Coverage**:
- Unit: N/A (minimal business logic in product listing)
- Integration: `CartService.AddToCartAsync` with real products
- E2E: Health check endpoint ensures application starts

**Critical Assertions**:
- Product must exist before adding to cart
- Cart line created with correct SKU, name, price, and quantity
- Duplicate SKU updates quantity instead of creating duplicate lines
- Invalid product ID throws `InvalidOperationException`

---

### ✅ Flow 2: Checkout → Order Creation
**Business Value**: Payment processing and order fulfillment  
**Test Coverage**:
- Unit: N/A (checkout is orchestration-heavy)
- Integration: `CheckoutService.CheckoutAsync` with mocked payment gateway
- E2E: `/api/checkout` endpoint returns order with status

**Critical Assertions**:
- Cart must exist to proceed with checkout
- Inventory is validated (sufficient stock) before payment
- Payment success creates order with "Paid" status
- Payment failure creates order with "Failed" status
- Order lines mirror cart lines (SKU, name, price, quantity)
- Cart is cleared after successful checkout
- **Known Issue**: Inventory is decremented before payment (tested as-is, will require fix in future)

---

### ✅ Flow 3: Order Retrieval
**Business Value**: Customer order history and support  
**Test Coverage**:
- Integration: N/A (no service layer, direct EF queries)
- E2E: `/api/orders/{id}` endpoint returns order with lines

**Critical Assertions**:
- Valid order ID returns order JSON
- Order includes all associated order lines
- Invalid order ID returns 404 Not Found

---

### ✅ Flow 4: Health Check
**Business Value**: Operational monitoring and readiness probes  
**Test Coverage**:
- E2E: Application starts successfully
- E2E: Database migrations apply automatically

**Critical Assertions**:
- Application responds to HTTP requests
- Database is accessible
- No startup exceptions

---

## Known Gaps and Limitations

The following scenarios are **NOT covered** in the initial test suite:

### ❌ Gap 1: Concurrent Inventory Modification
**Risk**: Race condition when multiple users checkout the same product simultaneously  
**Impact**: Potential overselling (inventory goes negative)  
**Mitigation**: Accept risk for initial migration; address with pessimistic locking or saga pattern in Phase 3  
**Technical Debt**: No optimistic concurrency control in `CheckoutService`

---

### ❌ Gap 2: Inventory Rollback on Payment Failure
**Risk**: Inventory is decremented *before* payment is charged (CheckoutService.cs:31)  
**Impact**: Failed payments cause permanent inventory loss  
**Mitigation**: Test documents current behavior; fix will be applied in Phase 0 of migration plan  
**Technical Debt**: Requires refactoring checkout transaction order

---

### ❌ Gap 3: Cart Abandonment Cleanup
**Risk**: Carts persist indefinitely, no TTL or cleanup mechanism  
**Impact**: Database bloat over time  
**Mitigation**: Out of scope for initial testing; address with background job in future  
**Technical Debt**: No expiration policy for guest carts

---

### ❌ Gap 4: Razor Page UI Tests
**Risk**: UI changes not validated by automated tests  
**Impact**: Manual testing required for page rendering  
**Mitigation**: Integration tests validate service layer; UI is thin presentation layer  
**Rationale**: Razor Pages have minimal logic; testing service layer provides sufficient coverage

---

### ❌ Gap 5: Multi-Customer Isolation
**Risk**: All tests use hardcoded "guest" customer ID  
**Impact**: Cannot validate multi-tenancy behavior  
**Mitigation**: Accept limitation; authentication is future enhancement  
**Technical Debt**: No user authentication system exists

---

### ❌ Gap 6: Product Listing and Filtering
**Risk**: Product catalog queries not integration tested  
**Impact**: Schema changes to Products table may break UI  
**Mitigation**: Low risk; Products table is stable; UI is read-only  
**Rationale**: No complex business logic in product listing

---

### ❌ Gap 7: Cart Quantity Updates
**Risk**: Updating cart line quantity (beyond initial add) not explicitly tested  
**Impact**: UI edge case not validated  
**Mitigation**: `AddToCartAsync` tests cover update path (lines 28-31 in CartService.cs)  
**Rationale**: Update logic is tested implicitly when adding duplicate SKU

---

### ❌ Gap 8: Error Handling and Observability
**Risk**: Exception scenarios not exhaustively tested  
**Impact**: Unknown behavior for edge cases (e.g., database timeout, deadlock)  
**Mitigation**: Core happy path and known failure modes are covered  
**Rationale**: Production monitoring and logging will detect anomalies

---

## Test Infrastructure

### Technology Stack

- **Test Framework**: xUnit 2.4.2+
- **Database**: SQL Server LocalDB (same as production)
- **Mocking**: Moq 4.18+ (for IPaymentGateway)
- **HTTP Testing**: Microsoft.AspNetCore.Mvc.Testing (WebApplicationFactory)
- **Assertions**: xUnit + FluentAssertions (optional, for readability)

### Test Project Structure

```
RetailMonolith.Tests/
├── Integration/
│   ├── CartServiceTests.cs          # Cart CRUD operations
│   ├── CheckoutServiceTests.cs      # Checkout flow with DB
│   └── TestDbContextFactory.cs      # In-memory DB helper
├── E2E/
│   ├── HealthCheckTests.cs          # Application startup
│   ├── CheckoutApiTests.cs          # POST /api/checkout
│   └── OrdersApiTests.cs            # GET /api/orders/{id}
├── Helpers/
│   └── TestDataBuilder.cs           # Seed test products/inventory
└── RetailMonolith.Tests.csproj
```

### Test Database Strategy

**Approach**: Isolated in-memory SQLite or ephemeral SQL Server database per test class

**Rationale**:
- Fast test execution (no shared state)
- No test interdependencies
- Full EF Core feature support (migrations, relationships)

**Implementation**:
- Each test creates a new `DbContext` with unique connection string
- Database is seeded with minimal test data (products + inventory)
- Database is disposed after test completion

**Alternative Considered**: Shared LocalDB instance with transaction rollback
- **Rejected**: Slower, potential for test pollution, harder to parallelize

---

## Continuous Integration (CI)

### GitHub Actions Workflow

**Trigger**: Pull requests to `main` branch  
**Jobs**:
1. **Build**: Compile application and test project
2. **Test**: Run all tests (unit + integration + E2E)
3. **Report**: Publish test results and coverage (optional)

**Success Criteria**:
- All tests pass (zero failures)
- Build succeeds without warnings
- No deployment unless CI is green

### Workflow File

Location: `.github/workflows/ci.yml`

**Key Steps**:
1. Checkout code
2. Setup .NET 9.0 SDK
3. Restore dependencies (`dotnet restore`)
4. Build solution (`dotnet build --no-restore`)
5. Run tests (`dotnet test --no-build --verbosity normal`)

**Timeout**: 10 minutes (fail fast)

---

## Test Maintenance Guidelines

### When to Update Tests

- **Production Code Change**: Update corresponding test immediately
- **New Feature**: Write tests first (TDD) or concurrently
- **Bug Fix**: Add regression test before fixing bug
- **Refactoring**: Tests should pass without modification (validates behavior preservation)

### Test Naming Convention

```csharp
[Fact]
public async Task MethodName_Scenario_ExpectedBehavior()
{
    // Arrange
    // Act
    // Result
}
```

**Example**:
```csharp
[Fact]
public async Task AddToCartAsync_DuplicateSku_IncrementsQuantity()
```

### Red Flags (Tests to Avoid)

❌ **Flaky Tests**: Tests that pass/fail non-deterministically (likely due to shared state)  
❌ **Slow Tests**: Integration tests taking >5 seconds (optimize queries or use in-memory DB)  
❌ **Brittle Tests**: Tests that break on unrelated changes (over-specified assertions)  
❌ **Tautological Tests**: Tests that duplicate production code logic (test behavior, not implementation)

---

## Success Metrics

### Coverage Targets

- **Service Layer**: 80%+ line coverage (CartService, CheckoutService)
- **Overall Application**: 60%+ line coverage (lower due to untested Razor Pages)
- **Critical Flows**: 100% of identified flows have at least one test

### Quality Metrics

- **Test Execution Time**: <30 seconds for full suite
- **Test Reliability**: Zero flaky tests
- **Build Success Rate**: 95%+ (allow for infrastructure transients)

### Non-Goals

- 100% code coverage (diminishing returns on non-critical paths)
- Testing third-party libraries (trust EF Core, ASP.NET Core)
- UI snapshot testing (Razor Pages are thin, low value)

---

## Rollback Capability

All tests are **read-only with respect to migration changes**. They validate the current monolith's behavior, ensuring that:

1. If a service extraction fails, tests continue to pass against the monolith
2. Tests can be run against both monolith and extracted services (contract tests)
3. No tests assume a specific deployment topology (containers, Kubernetes, etc.)

**Rollback Scenario**: If Phase 1 (Order Service extraction) fails, we revert routing to monolith and re-run CI. Tests should pass without modification.

---

## Phase 0 Testing Deliverables (This Phase)

### Completed
✅ Test Strategy document (this file)  
✅ Test project with xUnit infrastructure  
✅ Integration tests for `CartService`  
✅ Integration tests for `CheckoutService`  
✅ E2E tests for health check and startup  
✅ E2E tests for `/api/checkout` endpoint  
✅ E2E tests for `/api/orders/{id}` endpoint  
✅ GitHub Actions CI workflow  
✅ All tests passing against current monolith

### Future Enhancements (Post-Phase 0)
- Contract testing for extracted services (Phase 1+)
- Load testing for inventory race conditions (Phase 3)
- Chaos engineering for payment gateway failures (Phase 4)
- Security testing for authentication (future)

---

## Appendix: Test Execution Commands

### Run All Tests
```bash
dotnet test
```

### Run Specific Test Class
```bash
dotnet test --filter "FullyQualifiedName~CartServiceTests"
```

### Run with Coverage
```bash
dotnet test --collect:"XPlat Code Coverage"
```

### Watch Mode (TDD)
```bash
dotnet watch test
```

---

## Document History

| Date | Version | Author | Changes |
|------|---------|--------|---------|
| 2025-01-21 | 1.0 | Testing Agent | Initial test strategy for Phase 0 |

---

## Approval

- [ ] Development Team Lead
- [ ] QA Lead
- [ ] DevOps Engineer
- [ ] Modernisation Programme Manager

**Next Review Date**: After Phase 1 completion (estimated 2025-02-04)
