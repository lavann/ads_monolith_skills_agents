# ADR-003: Hardcoded Guest Customer ID for All Users

**Status**: Accepted (Implicit)  
**Date**: 2025-10-19 (inferred from initial migration date)  
**Context**: System Discovery Exercise

## Context

The application needs to associate carts and orders with users. Authentication and user management add significant complexity to the initial implementation.

## Decision

Use a single hardcoded customer ID (`"guest"`) for all users throughout the application. No authentication, no session management, no user identity resolution.

## Evidence

**Hardcoded "guest" throughout codebase**:
- `Models/Cart.cs:7`: `public string CustomerId { get; set; } = "guest";`
- `Models/Order.cs:7`: `public string CustomerId { get; set; } = "guest";`
- `Program.cs:53`: `var order = await svc.CheckoutAsync("guest", "tok_test");`
- `Pages/Products/Index.cshtml.cs:55,65`: `c.CustomerId == "guest"`
- `Pages/Cart/Index.cshtml.cs:32`: `await _cartService.GetCartWithLinesAsync("guest");`
- `Pages/Checkout/Index.cshtml.cs:31,46`: `"guest"`
- Comments: `Pages/Checkout/Index.cshtml.cs:19-21`: "For simplicity, using a hardcoded customer ID. In a real application, this would come from the authenticated user context or session."

## Consequences

### Positive
- **Zero authentication complexity**: No login, no password management, no session handling
- **Faster development**: Skip user management infrastructure entirely
- **Demo-friendly**: Anyone can access the app immediately without signup/login
- **Simplified testing**: No need to manage test users or authentication tokens

### Negative
- **No multi-user support**: All users share the same cart and see the same orders
- **No data isolation**: Any user can view and modify any other user's data (not applicable when all are "guest")
- **Cannot deploy to production**: No way to distinguish customers or protect data privacy
- **No user-specific features**: Cannot implement:
  - Order history per user
  - Saved addresses or payment methods
  - Wishlists or favorites
  - User preferences
- **GDPR/compliance risk**: Cannot identify data subjects for deletion requests

## Security Implications

The current implementation is **NOT SECURE** for any multi-user scenario. If deployed publicly:
1. All users would share a single shopping cart (last add wins, others lose items)
2. All users would see all orders from all other users (via `/Orders` page)
3. No authorization checks exist—any endpoint can be called by anyone

## Alternatives Considered

(None apparent in codebase; this was the simplest initial design)

**Recommended Alternatives**:
- **Session-based customer ID**: Generate unique customer ID per browser session (e.g., GUID in cookie)
- **ASP.NET Core Identity**: Add authentication with email/password
- **External identity provider**: Integrate with Azure AD, Auth0, etc.
- **API key authentication**: For headless/API scenarios

## Notes

The codebase includes a comment acknowledging this limitation (Pages/Checkout/Index.cshtml.cs:19-21), indicating that the hardcoded value is a known simplification, not an oversight.

This design is acceptable for:
- Local development
- Single-developer testing
- Proof-of-concept demonstrations

It is **NOT acceptable** for:
- Production deployment
- Shared development/staging environments (multiple users)
- Any scenario requiring data privacy or user isolation

**Blast Radius**: This decision impacts:
- `Cart` entity (CustomerId column)
- `Order` entity (CustomerId column)
- All services (`CartService`, `CheckoutService`)
- All Razor Pages (Products, Cart, Checkout, Orders)
- Both Minimal APIs (`/api/checkout`, `/api/orders/{id}`)

Migrating away from hardcoded "guest" will require changes across the entire codebase, including database schema changes (likely adding user tables, foreign keys, and row-level security).
