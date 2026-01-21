# ADR-004: Mock Payment Gateway for Checkout

**Status**: Accepted (Implicit)  
**Date**: 2025-10-19 (inferred from initial migration date)  
**Context**: System Discovery Exercise

## Context

The checkout flow requires integration with a payment processor to charge customers. Real payment gateway integration (Stripe, PayPal, etc.) requires:
- API credentials and keys
- Sandbox/test accounts
- Webhook handling for async payment confirmation
- PCI compliance considerations
- Error handling for declined cards, fraud detection, etc.

## Decision

Implement a mock payment gateway (`MockPaymentGateway`) that always returns success, allowing the checkout flow to be developed and tested without external dependencies.

## Evidence

**Services/MockPaymentGateway.cs:6-11**:
```csharp
public Task<PaymentResult> ChargeAsync(PaymentRequest req, CancellationToken ct = default)
{
    // trivial success for hack; add a random fail to demo error path if you like
    return Task.FromResult(new PaymentResult(true, $"MOCK-{Guid.NewGuid():N}", null));
}
```

**Services/IPaymentGateway.cs:1-12**: Interface definition with `PaymentRequest` and `PaymentResult` record types.

**Program.cs:16**: Registered as scoped service:
```csharp
builder.Services.AddScoped<IPaymentGateway, MockPaymentGateway>();
```

**CheckoutService.cs:10,35**: Injected and used:
```csharp
private readonly IPaymentGateway _payments;
var pay = await _payments.ChargeAsync(new(total, "GBP", paymentToken), ct);
```

## Consequences

### Positive
- **No external dependencies**: Development and testing do not require payment gateway accounts or API keys
- **Fast iteration**: No network latency or rate limiting from external APIs
- **No credentials in code**: No risk of leaking API keys in source control
- **Predictable behavior**: Always succeeds, simplifying happy-path testing
- **Easy to swap**: Interface abstraction allows replacing with real implementation later

### Negative
- **No real payment processing**: Orders are marked as "Paid" but no money is actually charged
- **Cannot test failure scenarios**: Always returns success, so payment failure handling is untested (except via manual code changes)
- **Comment suggests incompleteness**: Line 8 says "add a random fail to demo error path if you like", indicating error handling is not exercised
- **Not production-ready**: Must be replaced before any real deployment

## Security Implications

The mock implementation:
- **Does not validate** payment tokens (any string is accepted)
- **Does not perform** fraud detection
- **Does not enforce** PCI compliance
- **Does not handle** chargebacks or refunds

This is acceptable for development but **MUST NOT** be deployed to production.

## Failure Mode Analysis

The `CheckoutService` does check `PaymentResult.Succeeded` (CheckoutService.cs:36):
```csharp
var status = pay.Succeeded ? "Paid" : "Failed";
```

If a real gateway returns `Succeeded = false`, the order is created with `Status = "Failed"`. However:
- **Inventory is already decremented** (CheckoutService.cs:27-32, before payment)
- **Cart is still cleared** (CheckoutService.cs:51-52)
- **No rollback mechanism** for inventory or cart

This creates a data consistency issue: failed payments result in lost inventory and cleared carts with "Failed" orders.

## Alternatives Considered

(None apparent in codebase; this was the initial design)

**Recommended Alternatives**:
- **Stripe.net SDK**: Use Stripe's test mode for real integration testing
- **Configurable mock**: Allow injection of success/failure behavior via configuration or test setup
- **WireMock/HttpClient stub**: Simulate external API responses for integration tests

## Notes

The interface design is well-suited for dependency injection and testing:
- Clean abstraction with record types (`PaymentRequest`, `PaymentResult`)
- Async-first design with `CancellationToken` support
- Clear success/failure signaling via `Succeeded` boolean and optional `Error` message

**Replacement Path**: To integrate a real payment gateway:
1. Create new implementation of `IPaymentGateway` (e.g., `StripePaymentGateway`)
2. Register in `Program.cs` instead of `MockPaymentGateway`
3. Add API key configuration to `appsettings.json`
4. Implement retry logic and timeout handling
5. Add webhook endpoint for async payment confirmation (if required by gateway)

The abstraction layer makes this straightforward, but the inventory-before-payment issue in `CheckoutService` must be fixed first to prevent stock loss on payment failures.
