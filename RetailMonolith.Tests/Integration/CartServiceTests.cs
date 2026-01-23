using Microsoft.EntityFrameworkCore;
using RetailMonolith.Data;
using RetailMonolith.Services;
using RetailMonolith.Tests.Helpers;
using Xunit;

namespace RetailMonolith.Tests.Integration
{
    /// <summary>
    /// Integration tests for CartService with real database context.
    /// Tests validate cart CRUD operations and business logic.
    /// </summary>
    public class CartServiceTests : IDisposable
    {
        private readonly AppDbContext _context;
        private readonly CartService _cartService;
        private const string TestCustomerId = "test-customer";

        public CartServiceTests()
        {
            _context = TestDbContextFactory.CreateInMemoryDbContext(nameof(CartServiceTests));
            TestDbContextFactory.SeedTestData(_context).Wait();
            _cartService = new CartService(_context);
        }

        [Fact]
        public async Task GetOrCreateCartAsync_NewCustomer_CreatesNewCart()
        {
            // Arrange
            const string newCustomerId = "new-customer";

            // Act
            var cart = await _cartService.GetOrCreateCartAsync(newCustomerId);

            // Assert
            Assert.NotNull(cart);
            Assert.Equal(newCustomerId, cart.CustomerId);
            Assert.Empty(cart.Lines);
            Assert.True(cart.Id > 0, "Cart should be persisted with database ID");

            // Verify cart exists in database
            var dbCart = await _context.Carts.FindAsync(cart.Id);
            Assert.NotNull(dbCart);
        }

        [Fact]
        public async Task GetOrCreateCartAsync_ExistingCustomer_ReturnsExistingCart()
        {
            // Arrange - Create cart first
            var firstCart = await _cartService.GetOrCreateCartAsync(TestCustomerId);
            var firstCartId = firstCart.Id;

            // Act - Retrieve same cart
            var secondCart = await _cartService.GetOrCreateCartAsync(TestCustomerId);

            // Assert
            Assert.Equal(firstCartId, secondCart.Id);
            Assert.Equal(TestCustomerId, secondCart.CustomerId);
        }

        [Fact]
        public async Task AddToCartAsync_ValidProduct_AddsCartLine()
        {
            // Arrange
            const int productId = 1;

            // Act
            await _cartService.AddToCartAsync(TestCustomerId, productId, quantity: 2);

            // Assert
            var cart = await _context.Carts
                .Include(c => c.Lines)
                .FirstOrDefaultAsync(c => c.CustomerId == TestCustomerId);

            Assert.NotNull(cart);
            Assert.Single(cart.Lines);
            
            var line = cart.Lines.First();
            Assert.Equal("TEST-001", line.Sku);
            Assert.Equal("Test Product 1", line.Name);
            Assert.Equal(10.00m, line.UnitPrice);
            Assert.Equal(2, line.Quantity);
        }

        [Fact]
        public async Task AddToCartAsync_DuplicateSku_IncrementsQuantity()
        {
            // Arrange - Add product first time
            const int productId = 1;
            await _cartService.AddToCartAsync(TestCustomerId, productId, quantity: 3);

            // Act - Add same product again
            await _cartService.AddToCartAsync(TestCustomerId, productId, quantity: 2);

            // Assert
            var cart = await _context.Carts
                .Include(c => c.Lines)
                .FirstOrDefaultAsync(c => c.CustomerId == TestCustomerId);

            Assert.NotNull(cart);
            Assert.Single(cart.Lines); // Should still be one line
            
            var line = cart.Lines.First();
            Assert.Equal("TEST-001", line.Sku);
            Assert.Equal(5, line.Quantity); // 3 + 2 = 5
        }

        [Fact]
        public async Task AddToCartAsync_MultipleDifferentProducts_CreatesMultipleLines()
        {
            // Act
            await _cartService.AddToCartAsync(TestCustomerId, productId: 1, quantity: 1);
            await _cartService.AddToCartAsync(TestCustomerId, productId: 2, quantity: 2);

            // Assert
            var cart = await _context.Carts
                .Include(c => c.Lines)
                .FirstOrDefaultAsync(c => c.CustomerId == TestCustomerId);

            Assert.NotNull(cart);
            Assert.Equal(2, cart.Lines.Count);
            
            var line1 = cart.Lines.First(l => l.Sku == "TEST-001");
            Assert.Equal(1, line1.Quantity);
            Assert.Equal(10.00m, line1.UnitPrice);
            
            var line2 = cart.Lines.First(l => l.Sku == "TEST-002");
            Assert.Equal(2, line2.Quantity);
            Assert.Equal(20.00m, line2.UnitPrice);
        }

        [Fact]
        public async Task AddToCartAsync_InvalidProductId_ThrowsException()
        {
            // Arrange
            const int invalidProductId = 999;

            // Act & Assert
            var exception = await Assert.ThrowsAsync<InvalidOperationException>(
                () => _cartService.AddToCartAsync(TestCustomerId, invalidProductId)
            );

            Assert.Equal("Invalid product ID", exception.Message);
        }

        [Fact]
        public async Task GetCartWithLinesAsync_ExistingCart_ReturnsCartWithLines()
        {
            // Arrange - Create cart with items
            await _cartService.AddToCartAsync(TestCustomerId, productId: 1, quantity: 1);
            await _cartService.AddToCartAsync(TestCustomerId, productId: 2, quantity: 2);

            // Act
            var cart = await _cartService.GetCartWithLinesAsync(TestCustomerId);

            // Assert
            Assert.NotNull(cart);
            Assert.Equal(TestCustomerId, cart.CustomerId);
            Assert.Equal(2, cart.Lines.Count);
        }

        [Fact]
        public async Task GetCartWithLinesAsync_NoCart_ReturnsEmptyCartInstance()
        {
            // Arrange - No cart created
            const string nonExistentCustomerId = "non-existent";

            // Act
            var cart = await _cartService.GetCartWithLinesAsync(nonExistentCustomerId);

            // Assert
            Assert.NotNull(cart); // Should return empty instance, not null
            Assert.Equal(nonExistentCustomerId, cart.CustomerId);
            Assert.Empty(cart.Lines);
            Assert.Equal(0, cart.Id); // Not persisted
        }

        [Fact]
        public async Task ClearCartAsync_ExistingCart_DeletesCartAndLines()
        {
            // Arrange - Create cart with items
            await _cartService.AddToCartAsync(TestCustomerId, productId: 1, quantity: 1);
            await _cartService.AddToCartAsync(TestCustomerId, productId: 2, quantity: 2);
            
            var cartBeforeClear = await _context.Carts
                .Include(c => c.Lines)
                .FirstOrDefaultAsync(c => c.CustomerId == TestCustomerId);
            Assert.NotNull(cartBeforeClear);
            var cartId = cartBeforeClear.Id;

            // Act
            await _cartService.ClearCartAsync(TestCustomerId);

            // Assert - Cart should be deleted
            var cartAfterClear = await _context.Carts.FindAsync(cartId);
            Assert.Null(cartAfterClear);

            // Assert - Lines should be deleted (cascade)
            var linesCount = await _context.CartLines.CountAsync(l => l.CartId == cartId);
            Assert.Equal(0, linesCount);
        }

        [Fact]
        public async Task ClearCartAsync_NoCart_DoesNotThrow()
        {
            // Arrange - No cart exists
            const string nonExistentCustomerId = "non-existent";

            // Act & Assert - Should not throw
            await _cartService.ClearCartAsync(nonExistentCustomerId);
            
            // Verify no cart was created
            var cart = await _context.Carts
                .FirstOrDefaultAsync(c => c.CustomerId == nonExistentCustomerId);
            Assert.Null(cart);
        }

        public void Dispose()
        {
            _context.Database.EnsureDeleted();
            _context.Dispose();
        }
    }
}
