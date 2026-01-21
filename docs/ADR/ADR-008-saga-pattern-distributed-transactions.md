# ADR-008: Saga Pattern for Distributed Transactions

**Status**: Proposed  
**Date**: 2025-01-21  
**Context**: Modernisation Planning

## Context

The checkout flow in the monolith currently executes as a single local transaction:
1. Retrieve cart
2. Decrement inventory
3. Charge payment
4. Create order
5. Clear cart

When we extract services (Cart, Inventory, Payment, Order), this becomes a distributed transaction spanning multiple databases and services. We need a mechanism to maintain consistency across services while handling failures gracefully.

**Current Problem in Monolith**: Inventory is decremented BEFORE payment is charged (CheckoutService.cs:27-36). If payment fails, inventory is lost (critical bug).

**New Challenge**: After service extraction, we cannot use database transactions across services. We need a pattern for distributed consistency.

## Decision

Implement the **Saga Pattern with Orchestration** for the checkout flow, using the **Checkout Service as the saga orchestrator**.

### Saga Pattern Overview

A saga is a sequence of local transactions coordinated by an orchestrator:
- Each step commits a local transaction
- If a step fails, compensating transactions undo previous steps
- Orchestrator maintains saga state and coordinates steps

### Choreography vs Orchestration

| Approach | Description | Pros | Cons |
|----------|-------------|------|------|
| **Choreography** | Services publish events, others listen and react | Loose coupling, no central coordinator | Hard to debug, no visibility into saga state |
| **Orchestration** | Central orchestrator calls services in sequence | Clear control flow, easy to debug | Orchestrator can become bottleneck |

**Decision: Orchestration** for our use case because:
- ✅ Simpler to understand and debug (centralized logic)
- ✅ Easier to test (single orchestrator service)
- ✅ Better visibility (orchestrator logs entire saga flow)
- ✅ Checkout flow is linear (not a complex graph)

## Checkout Saga Implementation

### Saga Steps

```
┌─────────────────────────────────────────────────────────────┐
│                    Checkout Saga                            │
│                (Checkout Service = Orchestrator)            │
└───┬─────────────────────────────────────────────────────────┘
    │
    ├─> Step 1: Get Cart (Cart Service)
    │     └─> Compensate: N/A (read-only)
    │
    ├─> Step 2: Reserve Inventory (Inventory Service)
    │     └─> Compensate: Release Reservation
    │
    ├─> Step 3: Charge Payment (Payment Gateway)
    │     └─> Compensate: Refund Payment
    │
    ├─> Step 4: Commit Inventory (Inventory Service)
    │     └─> Compensate: N/A (cannot undo)
    │
    ├─> Step 5: Create Order (Order Service)
    │     └─> Compensate: Mark Order as "Cancelled"
    │
    └─> Step 6: Clear Cart (Cart Service)
          └─> Compensate: N/A (acceptable to lose cart on failure)
```

### Inventory Reservation (Two-Phase)

**Problem**: Need to ensure inventory is available BEFORE charging payment, but COMMIT inventory only AFTER payment succeeds.

**Solution**: Two-phase inventory reservation

**Phase 1: Reserve** (pre-payment)
- Lock inventory without decrementing quantity
- Create reservation record with expiry time (10 minutes)
- Return reservation ID (idempotency key)

**Phase 2a: Commit** (payment succeeded)
- Decrement inventory quantity
- Mark reservation as "Committed"

**Phase 2b: Release** (payment failed)
- Cancel reservation
- Mark reservation as "Released"
- Quantity remains unchanged

### Idempotency

All saga steps must be **idempotent** (safe to retry):

**Reserve Inventory** - idempotent via reservation ID:
```csharp
POST /api/inventory/{sku}/reserve
Body: { "quantity": 2, "reservationId": "abc-123" }

// If called twice with same reservationId, returns existing reservation
```

**Charge Payment** - idempotent via transaction ID:
```csharp
POST /api/payments/charge
Body: { "amount": 49.99, "token": "tok_123", "idempotencyKey": "xyz-789" }

// Payment gateway checks idempotency key, doesn't charge twice
```

**Create Order** - idempotent via order ID:
```csharp
POST /api/orders
Body: { "orderId": "order-456", "customerId": "guest", ... }

// If called twice with same orderId, returns existing order
```

### Compensating Transactions

If a step fails, orchestrator calls compensating transactions for completed steps:

**Scenario: Payment Fails**

1. ✅ Cart retrieved
2. ✅ Inventory reserved (reservation ID: R1)
3. ❌ Payment failed (insufficient funds)
4. **Compensate**: Release inventory reservation R1
5. Return error to user: "Payment failed: Insufficient funds"

**Scenario: Order Creation Fails**

1. ✅ Cart retrieved
2. ✅ Inventory reserved (R1)
3. ✅ Payment charged (transaction TX1)
4. ✅ Inventory committed (stock decremented)
5. ❌ Order creation failed (database error)
6. **Compensate**: Refund payment TX1, create "manual intervention" log entry
7. Return error to user: "Checkout failed, please contact support"

**Note**: After inventory committed, we cannot reliably rollback (stock already decremented). This requires manual intervention or eventual consistency patterns.

## Implementation

### Checkout Service (Orchestrator)

```csharp
public class CheckoutSagaOrchestrator
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<CheckoutSagaOrchestrator> _logger;

    public async Task<SagaResult<Order>> ExecuteCheckoutSaga(
        string customerId, 
        string paymentToken, 
        CancellationToken ct)
    {
        var sagaId = Guid.NewGuid();
        var sagaState = new CheckoutSagaState
        {
            SagaId = sagaId,
            CustomerId = customerId,
            Status = "Started",
            StartedAt = DateTime.UtcNow
        };

        _logger.LogInformation("Starting checkout saga {SagaId} for customer {CustomerId}", 
            sagaId, customerId);

        try
        {
            // Step 1: Get Cart
            sagaState.Cart = await GetCartAsync(customerId, ct);
            if (sagaState.Cart == null || !sagaState.Cart.Lines.Any())
            {
                return SagaResult<Order>.Failure("Cart is empty");
            }
            
            sagaState.Total = sagaState.Cart.Lines.Sum(l => l.UnitPrice * l.Quantity);
            _logger.LogInformation("Saga {SagaId}: Cart retrieved with {LineCount} items, total {Total}", 
                sagaId, sagaState.Cart.Lines.Count, sagaState.Total);

            // Step 2: Reserve Inventory
            var reservationIds = await ReserveInventoryAsync(
                sagaState.Cart.Lines, sagaId, ct);
            sagaState.ReservationIds = reservationIds;
            _logger.LogInformation("Saga {SagaId}: Inventory reserved ({ReservationCount} reservations)", 
                sagaId, reservationIds.Count);

            // Step 3: Charge Payment
            var paymentResult = await ChargePaymentAsync(
                sagaState.Total, paymentToken, sagaId, ct);
            
            if (!paymentResult.Succeeded)
            {
                // Compensate: Release inventory
                _logger.LogWarning("Saga {SagaId}: Payment failed, compensating", sagaId);
                await ReleaseInventoryReservationsAsync(reservationIds, ct);
                return SagaResult<Order>.Failure($"Payment failed: {paymentResult.Error}");
            }
            
            sagaState.PaymentTransactionId = paymentResult.TransactionId;
            _logger.LogInformation("Saga {SagaId}: Payment charged (transaction {TxId})", 
                sagaId, paymentResult.TransactionId);

            // Step 4: Commit Inventory (no rollback after this point)
            await CommitInventoryReservationsAsync(reservationIds, ct);
            _logger.LogInformation("Saga {SagaId}: Inventory committed", sagaId);

            // Step 5: Create Order
            Order order;
            try
            {
                order = await CreateOrderAsync(
                    customerId, sagaState.Cart, sagaState.Total, sagaId, ct);
                _logger.LogInformation("Saga {SagaId}: Order created (order {OrderId})", 
                    sagaId, order.Id);
            }
            catch (Exception ex)
            {
                // Critical failure: Payment charged, inventory decremented, but order not created
                // Compensate: Refund payment (if supported by gateway)
                _logger.LogError(ex, 
                    "Saga {SagaId}: Order creation failed after payment, manual intervention required", 
                    sagaId);
                
                await RefundPaymentAsync(paymentResult.TransactionId, sagaState.Total, ct);
                
                // Log for manual reconciliation
                await LogManualInterventionRequiredAsync(sagaId, sagaState, ex);
                
                return SagaResult<Order>.Failure(
                    "Checkout failed after payment. Support has been notified. Reference: " + sagaId);
            }

            // Step 6: Clear Cart
            await ClearCartAsync(customerId, ct);
            _logger.LogInformation("Saga {SagaId}: Cart cleared", sagaId);

            sagaState.Status = "Completed";
            sagaState.CompletedAt = DateTime.UtcNow;
            
            _logger.LogInformation("Saga {SagaId}: Completed successfully in {Duration}ms", 
                sagaId, (sagaState.CompletedAt.Value - sagaState.StartedAt).TotalMilliseconds);

            return SagaResult<Order>.Success(order);
        }
        catch (Exception ex)
        {
            // Unhandled exception: Attempt best-effort compensation
            _logger.LogError(ex, "Saga {SagaId}: Unhandled exception, attempting compensation", 
                sagaId);
            
            await CompensateAsync(sagaState, ct);
            
            return SagaResult<Order>.Failure("Checkout failed: " + ex.Message);
        }
    }

    private async Task<List<Guid>> ReserveInventoryAsync(
        IEnumerable<CartLine> lines, 
        Guid sagaId, 
        CancellationToken ct)
    {
        var inventoryClient = _httpClientFactory.CreateClient("InventoryService");
        var reservationIds = new List<Guid>();

        foreach (var line in lines)
        {
            var reservationId = Guid.NewGuid();
            
            var response = await inventoryClient.PostAsJsonAsync(
                $"/api/inventory/{line.Sku}/reserve",
                new
                {
                    quantity = line.Quantity,
                    reservationId,
                    sagaId
                },
                ct);

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync(ct);
                throw new SagaStepFailedException(
                    $"Inventory reservation failed for SKU {line.Sku}: {error}");
            }

            reservationIds.Add(reservationId);
        }

        return reservationIds;
    }

    private async Task ReleaseInventoryReservationsAsync(
        List<Guid> reservationIds, 
        CancellationToken ct)
    {
        var inventoryClient = _httpClientFactory.CreateClient("InventoryService");

        foreach (var reservationId in reservationIds)
        {
            try
            {
                await inventoryClient.PostAsync(
                    $"/api/inventory/release?reservationId={reservationId}",
                    null,
                    ct);
            }
            catch (Exception ex)
            {
                // Log but don't fail (best-effort compensation)
                _logger.LogWarning(ex, 
                    "Failed to release reservation {ReservationId} (non-critical)", 
                    reservationId);
            }
        }
    }

    private async Task CommitInventoryReservationsAsync(
        List<Guid> reservationIds, 
        CancellationToken ct)
    {
        var inventoryClient = _httpClientFactory.CreateClient("InventoryService");

        foreach (var reservationId in reservationIds)
        {
            var response = await inventoryClient.PostAsync(
                $"/api/inventory/commit?reservationId={reservationId}",
                null,
                ct);

            response.EnsureSuccessStatusCode();
        }
    }

    // Similar methods for other steps...
}

public class CheckoutSagaState
{
    public Guid SagaId { get; set; }
    public string CustomerId { get; set; }
    public string Status { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public Cart Cart { get; set; }
    public decimal Total { get; set; }
    public List<Guid> ReservationIds { get; set; } = new();
    public string PaymentTransactionId { get; set; }
}

public class SagaResult<T>
{
    public bool Succeeded { get; set; }
    public T Value { get; set; }
    public string Error { get; set; }

    public static SagaResult<T> Success(T value) => 
        new() { Succeeded = true, Value = value };
    
    public static SagaResult<T> Failure(string error) => 
        new() { Succeeded = false, Error = error };
}

public class SagaStepFailedException : Exception
{
    public SagaStepFailedException(string message) : base(message) { }
}
```

## Consequences

### Positive

- ✅ **Fixes inventory-payment bug**: Inventory reserved BEFORE payment
- ✅ **Graceful failure handling**: Compensating transactions undo partial work
- ✅ **Idempotent operations**: Safe to retry failed steps
- ✅ **Centralized coordination**: Easy to debug and monitor
- ✅ **Audit trail**: Saga logs provide complete execution history

### Negative

- ❌ **Complexity**: More code than simple transaction
- ❌ **Partial failure states**: Between commit inventory and create order
- ❌ **Eventual consistency**: Compensating transactions take time
- ❌ **No guarantees**: Compensating transactions can also fail
- ❌ **Manual intervention**: Some failures require human resolution

### Risks

| Risk | Likelihood | Impact | Mitigation |
|------|-----------|--------|------------|
| **Compensating transaction fails** | Low | Medium | Retry logic, dead letter queue, manual intervention log |
| **Saga takes too long (timeout)** | Low | Medium | Set reasonable timeouts, async processing for long-running steps |
| **Partial failure (after commit)** | Low | High | Manual reconciliation tools, alert on critical failures |
| **Inventory reservation expires** | Medium | Low | 10-minute expiry, clean up expired reservations via background job |

## Alternatives Considered

### Alternative 1: Two-Phase Commit (2PC)

**Description**: Distributed transaction protocol with prepare/commit phases

**Pros**: Strong consistency, ACID guarantees  
**Cons**: Requires all participants support 2PC, locks held during transaction, high latency

**Rejected**: Not supported by HTTP services, too complex, risk of blocking

### Alternative 2: Event-Driven Choreography

**Description**: Services publish events, others listen and react

**Pros**: Loose coupling, no central coordinator  
**Cons**: Hard to debug, no visibility into saga state, complex failure handling

**Rejected**: Checkout flow is linear, orchestration simpler for our use case

### Alternative 3: Compensating Transaction Framework (MassTransit Saga)

**Description**: Use library (MassTransit, NServiceBus) for saga management

**Pros**: Mature, battle-tested, state persistence  
**Cons**: New dependency, learning curve, additional infrastructure (message bus)

**Deferred**: Consider for Phase 4+ if saga complexity increases

### Alternative 4: Keep Checkout in Monolith

**Description**: Don't extract checkout, keep as single transaction

**Pros**: Simple, ACID transactions  
**Cons**: Defeats purpose of microservices, cannot scale checkout independently

**Rejected**: Goal is to extract all services, checkout is core business logic

## Validation

### Success Criteria

- ✅ Checkout saga completes successfully (happy path)
- ✅ Payment failure releases inventory (no stock loss)
- ✅ Compensating transactions execute on failure
- ✅ Idempotent operations safe to retry
- ✅ Saga logs provide complete audit trail
- ✅ Response time < 1000ms for complete checkout

### Testing Strategy

**Unit Tests**: Saga orchestrator logic
```csharp
[Fact]
public async Task Saga_CompensatesOnPaymentFailure()
{
    // Mock services
    var mockInventory = Mock.Of<IInventoryService>();
    var mockPayment = Mock.Of<IPaymentGateway>();
    
    // Setup: Payment fails
    Mock.Get(mockPayment)
        .Setup(x => x.ChargeAsync(It.IsAny<decimal>(), It.IsAny<string>()))
        .ReturnsAsync(new PaymentResult { Succeeded = false, Error = "Insufficient funds" });
    
    // Execute saga
    var orchestrator = new CheckoutSagaOrchestrator(...);
    var result = await orchestrator.ExecuteCheckoutSaga("customer1", "token");
    
    // Assert: Inventory released
    Assert.False(result.Succeeded);
    Mock.Get(mockInventory).Verify(
        x => x.ReleaseReservationAsync(It.IsAny<Guid>()), 
        Times.Once);
}
```

**Integration Tests**: End-to-end saga execution
```csharp
[Fact]
public async Task Saga_CompletesSuccessfully()
{
    // Setup: Real services (test containers)
    var cart = await CreateTestCartAsync("customer1");
    
    // Execute saga
    var result = await _checkoutClient.PostAsJsonAsync("/api/checkout", new
    {
        customerId = "customer1",
        paymentToken = "tok_test"
    });
    
    // Assert: Order created
    var order = await result.Content.ReadFromJsonAsync<Order>();
    Assert.NotNull(order);
    Assert.Equal("Paid", order.Status);
    
    // Assert: Inventory decremented
    var inventory = await _inventoryClient.GetFromJsonAsync<InventoryItem>($"/api/inventory/{cart.Lines[0].Sku}");
    Assert.Equal(originalQuantity - cart.Lines[0].Quantity, inventory.Quantity);
    
    // Assert: Cart cleared
    var cartAfter = await _cartClient.GetFromJsonAsync<Cart>($"/api/carts/customer1");
    Assert.Empty(cartAfter.Lines);
}
```

**Chaos Tests**: Failure scenarios
```csharp
[Fact]
public async Task Saga_HandlesServiceFailure()
{
    // Simulate: Inventory service down
    await StopInventoryServiceAsync();
    
    // Execute saga
    var result = await _checkoutClient.PostAsJsonAsync("/api/checkout", ...);
    
    // Assert: Returns error, no partial state
    Assert.Equal(HttpStatusCode.ServiceUnavailable, result.StatusCode);
    
    // Assert: No orders created
    var orders = await _orderClient.GetFromJsonAsync<List<Order>>("/api/orders");
    Assert.Empty(orders);
}
```

## References

- [Saga Pattern - Chris Richardson](https://microservices.io/patterns/data/saga.html)
- [Compensating Transaction Pattern - Microsoft](https://learn.microsoft.com/en-us/azure/architecture/patterns/compensating-transaction)
- [Designing Distributed Systems - Brendan Burns (Chapter 4)](https://www.oreilly.com/library/view/designing-distributed-systems/9781491983638/)
- [Building Event-Driven Microservices - Sam Newman](https://samnewman.io/talks/building-event-driven-microservices/)

## Related ADRs

- ADR-005: Service Decomposition Strategy
- ADR-006: Data Migration Strategy
- ADR-007: API Gateway Technology Selection (YARP)
