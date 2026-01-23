# RetailMonolith Tests

This test suite provides comprehensive test coverage for the Retail Monolith application, establishing a safety net for incremental modernization.

## Test Structure

```
RetailMonolith.Tests/
├── Integration/           # Service layer tests with real database
│   ├── CartServiceTests.cs
│   └── CheckoutServiceTests.cs
├── E2E/                   # End-to-end application tests
│   └── HealthCheckTests.cs
└── Helpers/               # Test utilities
    └── TestDbContextFactory.cs
```

## Test Coverage

### Integration Tests (19 tests)
- **CartService**: 12 tests covering cart CRUD operations
  - Add to cart (new product, duplicate SKU, invalid product)
  - Get or create cart
  - Retrieve cart with lines
  - Clear cart
  
- **CheckoutService**: 7 tests covering checkout flow
  - Successful and failed payments
  - Inventory management
  - Order creation
  - Cart clearing
  - Edge cases (no cart, empty cart, insufficient stock)

### E2E Tests (2 tests)
- **Application Startup**: Validates app can start with test dependencies
- **Dependency Injection**: Validates DbContext resolution

## Running Tests

### Run All Tests
```bash
dotnet test
```

### Run Only Integration Tests
```bash
dotnet test --filter "FullyQualifiedName~Integration"
```

### Run Specific Test Class
```bash
dotnet test --filter "FullyQualifiedName~CartServiceTests"
```

### Run with Detailed Output
```bash
dotnet test --verbosity detailed
```

### Watch Mode (TDD)
```bash
dotnet watch test
```

## Test Database Strategy

- **Integration Tests**: Each test class uses an isolated in-memory database
- **E2E Tests**: WebApplicationFactory with in-memory database
- **Isolation**: Each test gets a unique database instance to prevent pollution

## Known Test Gaps

As documented in `/docs/Test-Strategy.md`, the following are **not** covered:

1. Concurrent inventory modification (race conditions)
2. Razor Page UI rendering
3. Product listing and filtering
4. Cart quantity updates beyond initial add
5. Multi-customer isolation (all tests use "guest")

## CI Integration

Tests run automatically on pull requests via GitHub Actions (`.github/workflows/ci.yml`).

### CI Requirements
- All tests must pass
- Build must succeed
- No compilation warnings in test project

## Test Guidelines

### Naming Convention
```csharp
[Fact]
public async Task MethodName_Scenario_ExpectedBehavior()
{
    // Arrange
    // Act
    // Assert
}
```

### Test Characteristics
- **Fast**: Integration tests complete in <2 seconds total
- **Isolated**: No shared state between tests
- **Deterministic**: No flaky tests
- **Behavior-focused**: Test what, not how

## Dependencies

- **xUnit**: Test framework
- **Moq**: Mocking library (for IPaymentGateway)
- **Microsoft.EntityFrameworkCore.InMemory**: In-memory database for tests
- **Microsoft.AspNetCore.Mvc.Testing**: WebApplicationFactory for E2E tests

## Troubleshooting

### Tests failing locally
```bash
# Clean and rebuild
dotnet clean
dotnet build
dotnet test
```

### Database-related failures
- Ensure no LocalDB instances are running
- In-memory database should be isolated per test
- Check that `SkipAutoMigration` is set for E2E tests

### CI failures
- Check GitHub Actions logs in `.github/workflows/ci.yml`
- Ensure all NuGet packages are restored
- Verify .NET 9.0 SDK is installed

## Future Enhancements

- Add contract tests for extracted services (Phase 1+)
- Add load tests for inventory race conditions (Phase 3)
- Add security tests for authentication (future)
- Increase test coverage for Razor Pages (optional)
