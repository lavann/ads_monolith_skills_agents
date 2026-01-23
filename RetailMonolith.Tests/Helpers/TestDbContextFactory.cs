using Microsoft.EntityFrameworkCore;
using RetailMonolith.Data;
using RetailMonolith.Models;

namespace RetailMonolith.Tests.Helpers
{
    /// <summary>
    /// Helper class for creating isolated test database contexts.
    /// Each test gets a unique in-memory database to avoid test pollution.
    /// </summary>
    public static class TestDbContextFactory
    {
        /// <summary>
        /// Creates a new DbContext with an in-memory database for testing.
        /// </summary>
        /// <param name="testName">Unique name for this test (prevents database sharing between tests)</param>
        public static AppDbContext CreateInMemoryDbContext(string testName)
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(databaseName: $"TestDb_{testName}_{Guid.NewGuid()}")
                .Options;

            var context = new AppDbContext(options);
            context.Database.EnsureCreated();
            return context;
        }

        /// <summary>
        /// Seeds test data: 3 products with matching inventory.
        /// </summary>
        public static async Task SeedTestData(AppDbContext context)
        {
            var products = new[]
            {
                new Product
                {
                    Id = 1,
                    Sku = "TEST-001",
                    Name = "Test Product 1",
                    Description = "Test Description 1",
                    Price = 10.00m,
                    Currency = "GBP",
                    IsActive = true,
                    Category = "Electronics"
                },
                new Product
                {
                    Id = 2,
                    Sku = "TEST-002",
                    Name = "Test Product 2",
                    Description = "Test Description 2",
                    Price = 20.00m,
                    Currency = "GBP",
                    IsActive = true,
                    Category = "Apparel"
                },
                new Product
                {
                    Id = 3,
                    Sku = "TEST-003",
                    Name = "Test Product 3",
                    Description = "Test Description 3",
                    Price = 30.00m,
                    Currency = "GBP",
                    IsActive = false, // Inactive product
                    Category = "Home"
                }
            };

            var inventory = new[]
            {
                new InventoryItem { Sku = "TEST-001", Quantity = 100 },
                new InventoryItem { Sku = "TEST-002", Quantity = 50 },
                new InventoryItem { Sku = "TEST-003", Quantity = 25 }
            };

            context.Products.AddRange(products);
            context.Inventory.AddRange(inventory);
            await context.SaveChangesAsync();
        }
    }
}
