using Microsoft.EntityFrameworkCore;
using Moq;
using RetailMonolith.Data;
using RetailMonolith.Services;
using RetailMonolith.Tests.Helpers;
using Xunit;

namespace RetailMonolith.Tests.Integration
{
    /// <summary>
    /// Integration tests for CheckoutService with real database and mocked payment gateway.
    /// Tests validate end-to-end checkout flow including inventory management and order creation.
    /// </summary>
    public class CheckoutServiceTests : IDisposable
    {
        private readonly AppDbContext _context;
        private readonly Mock<IPaymentGateway> _mockPaymentGateway;
        private readonly CheckoutService _checkoutService;
        private readonly CartService _cartService;
        private const string TestCustomerId = "test-customer";

        public CheckoutServiceTests()
        {
            _context = TestDbContextFactory.CreateInMemoryDbContext(nameof(CheckoutServiceTests));
            TestDbContextFactory.SeedTestData(_context).Wait();
            
            _mockPaymentGateway = new Mock<IPaymentGateway>();
            _checkoutService = new CheckoutService(_context, _mockPaymentGateway.Object);
            _cartService = new CartService(_context);
        }

        [Fact]
        public async Task CheckoutAsync_SuccessfulPayment_CreatesOrderWithPaidStatus()
        {
            // Arrange - Create cart with items
            await _cartService.AddToCartAsync(TestCustomerId, productId: 1, quantity: 2);
            await _cartService.AddToCartAsync(TestCustomerId, productId: 2, quantity: 1);
            
            var expectedTotal = (10.00m * 2) + (20.00m * 1); // 40.00

            // Mock payment gateway to return success
            _mockPaymentGateway
                .Setup(p => p.ChargeAsync(It.IsAny<PaymentRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new PaymentResult(true, "MOCK-TXN-123", null));

            // Act
            var order = await _checkoutService.CheckoutAsync(TestCustomerId, "tok_test");

            // Assert - Order created correctly
            Assert.NotNull(order);
            Assert.Equal(TestCustomerId, order.CustomerId);
            Assert.Equal("Paid", order.Status);
            Assert.Equal(expectedTotal, order.Total);
            Assert.Equal(2, order.Lines.Count);

            // Verify order lines
            var line1 = order.Lines.First(l => l.Sku == "TEST-001");
            Assert.Equal("Test Product 1", line1.Name);
            Assert.Equal(10.00m, line1.UnitPrice);
            Assert.Equal(2, line1.Quantity);

            var line2 = order.Lines.First(l => l.Sku == "TEST-002");
            Assert.Equal("Test Product 2", line2.Name);
            Assert.Equal(20.00m, line2.UnitPrice);
            Assert.Equal(1, line2.Quantity);

            // Verify payment was called with correct parameters
            _mockPaymentGateway.Verify(
                p => p.ChargeAsync(
                    It.Is<PaymentRequest>(r => r.Amount == expectedTotal && r.Currency == "GBP" && r.Token == "tok_test"),
                    It.IsAny<CancellationToken>()
                ),
                Times.Once
            );
        }

        [Fact]
        public async Task CheckoutAsync_FailedPayment_CreatesOrderWithFailedStatus()
        {
            // Arrange - Create cart with items
            await _cartService.AddToCartAsync(TestCustomerId, productId: 1, quantity: 1);
            
            // Mock payment gateway to return failure
            _mockPaymentGateway
                .Setup(p => p.ChargeAsync(It.IsAny<PaymentRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new PaymentResult(false, null, "Insufficient funds"));

            // Act
            var order = await _checkoutService.CheckoutAsync(TestCustomerId, "tok_test");

            // Assert
            Assert.NotNull(order);
            Assert.Equal("Failed", order.Status);
            Assert.Equal(10.00m, order.Total);
        }

        [Fact]
        public async Task CheckoutAsync_Success_ClearsCart()
        {
            // Arrange - Create cart with items
            await _cartService.AddToCartAsync(TestCustomerId, productId: 1, quantity: 1);
            
            _mockPaymentGateway
                .Setup(p => p.ChargeAsync(It.IsAny<PaymentRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new PaymentResult(true, "MOCK-TXN-123", null));

            // Act
            await _checkoutService.CheckoutAsync(TestCustomerId, "tok_test");

            // Assert - Cart lines should be deleted (current implementation only deletes lines, not cart entity)
            var cart = await _context.Carts
                .Include(c => c.Lines)
                .FirstOrDefaultAsync(c => c.CustomerId == TestCustomerId);
            
            // Current implementation: cart entity remains, but lines are cleared
            Assert.NotNull(cart);
            Assert.Empty(cart.Lines);
        }

        [Fact]
        public async Task CheckoutAsync_Success_DecrementsInventory()
        {
            // Arrange - Create cart with items
            await _cartService.AddToCartAsync(TestCustomerId, productId: 1, quantity: 5);
            await _cartService.AddToCartAsync(TestCustomerId, productId: 2, quantity: 3);
            
            var inventoryBefore1 = await _context.Inventory.FirstAsync(i => i.Sku == "TEST-001");
            var inventoryBefore2 = await _context.Inventory.FirstAsync(i => i.Sku == "TEST-002");
            var initialQuantity1 = inventoryBefore1.Quantity;
            var initialQuantity2 = inventoryBefore2.Quantity;

            _mockPaymentGateway
                .Setup(p => p.ChargeAsync(It.IsAny<PaymentRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new PaymentResult(true, "MOCK-TXN-123", null));

            // Act
            await _checkoutService.CheckoutAsync(TestCustomerId, "tok_test");

            // Assert - Inventory decremented
            var inventoryAfter1 = await _context.Inventory.FirstAsync(i => i.Sku == "TEST-001");
            var inventoryAfter2 = await _context.Inventory.FirstAsync(i => i.Sku == "TEST-002");
            
            Assert.Equal(initialQuantity1 - 5, inventoryAfter1.Quantity);
            Assert.Equal(initialQuantity2 - 3, inventoryAfter2.Quantity);
        }

        [Fact]
        public async Task CheckoutAsync_InsufficientStock_ThrowsException()
        {
            // Arrange - Try to order more than available (inventory has 100 of TEST-001)
            await _cartService.AddToCartAsync(TestCustomerId, productId: 1, quantity: 200);

            _mockPaymentGateway
                .Setup(p => p.ChargeAsync(It.IsAny<PaymentRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new PaymentResult(true, "MOCK-TXN-123", null));

            // Act & Assert
            var exception = await Assert.ThrowsAsync<InvalidOperationException>(
                () => _checkoutService.CheckoutAsync(TestCustomerId, "tok_test")
            );

            Assert.Contains("Out of stock", exception.Message);
            Assert.Contains("TEST-001", exception.Message);
            
            // Verify payment was NOT called (validation happens before payment)
            _mockPaymentGateway.Verify(
                p => p.ChargeAsync(It.IsAny<PaymentRequest>(), It.IsAny<CancellationToken>()),
                Times.Never
            );
        }

        [Fact]
        public async Task CheckoutAsync_NoCart_ThrowsException()
        {
            // Arrange - No cart exists
            const string nonExistentCustomerId = "non-existent";

            // Act & Assert
            var exception = await Assert.ThrowsAsync<InvalidOperationException>(
                () => _checkoutService.CheckoutAsync(nonExistentCustomerId, "tok_test")
            );

            Assert.Equal("Cart not found", exception.Message);
            
            // Verify payment was NOT called
            _mockPaymentGateway.Verify(
                p => p.ChargeAsync(It.IsAny<PaymentRequest>(), It.IsAny<CancellationToken>()),
                Times.Never
            );
        }

        [Fact]
        public async Task CheckoutAsync_EmptyCart_ThrowsException()
        {
            // Arrange - Create cart but don't add items
            await _cartService.GetOrCreateCartAsync(TestCustomerId);

            // Act & Assert
            // Empty cart will throw NullReferenceException on line 36 of CheckoutService (payment result access)
            // This documents current behavior - empty cart is not explicitly validated
            await Assert.ThrowsAsync<NullReferenceException>(
                () => _checkoutService.CheckoutAsync(TestCustomerId, "tok_test")
            );
        }

        [Fact]
        public async Task CheckoutAsync_FailedPayment_StillDecrementsInventory()
        {
            // This test documents the KNOWN ISSUE: Inventory is decremented before payment
            // This is a bug in the current implementation that needs fixing
            
            // Arrange - Create cart with items
            await _cartService.AddToCartAsync(TestCustomerId, productId: 1, quantity: 5);
            
            var inventoryBefore = await _context.Inventory.FirstAsync(i => i.Sku == "TEST-001");
            var initialQuantity = inventoryBefore.Quantity;

            _mockPaymentGateway
                .Setup(p => p.ChargeAsync(It.IsAny<PaymentRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new PaymentResult(false, null, "Payment declined"));

            // Act
            var order = await _checkoutService.CheckoutAsync(TestCustomerId, "tok_test");

            // Assert - Order status is Failed
            Assert.Equal("Failed", order.Status);

            // KNOWN ISSUE: Inventory is still decremented even though payment failed
            var inventoryAfter = await _context.Inventory.FirstAsync(i => i.Sku == "TEST-001");
            Assert.Equal(initialQuantity - 5, inventoryAfter.Quantity);
            
            // This is documented in Test Strategy as a known gap (Gap 2)
            // Will be fixed in Phase 0 of migration plan
        }

        [Fact]
        public async Task CheckoutAsync_OrderPersistedToDatabase()
        {
            // Arrange - Create cart with items
            await _cartService.AddToCartAsync(TestCustomerId, productId: 1, quantity: 1);
            
            _mockPaymentGateway
                .Setup(p => p.ChargeAsync(It.IsAny<PaymentRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new PaymentResult(true, "MOCK-TXN-123", null));

            // Act
            var order = await _checkoutService.CheckoutAsync(TestCustomerId, "tok_test");

            // Assert - Order exists in database
            var dbOrder = await _context.Orders
                .Include(o => o.Lines)
                .FirstOrDefaultAsync(o => o.Id == order.Id);
            
            Assert.NotNull(dbOrder);
            Assert.Equal("Paid", dbOrder.Status);
            Assert.Single(dbOrder.Lines);
        }

        public void Dispose()
        {
            _context.Database.EnsureDeleted();
            _context.Dispose();
        }
    }
}
