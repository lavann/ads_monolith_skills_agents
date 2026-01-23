using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OrderService.Data;
using OrderService.Models;
using System.Net;
using System.Net.Http.Json;

namespace OrderService.Tests;

public class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            // Remove all existing DbContext related registrations
            var descriptors = services.Where(d =>
                d.ServiceType == typeof(DbContextOptions<OrderDbContext>) ||
                d.ServiceType == typeof(DbContextOptions) ||
                d.ImplementationType?.FullName?.Contains("OrderDbContext") == true)
                .ToList();

            foreach (var descriptor in descriptors)
            {
                services.Remove(descriptor);
            }

            // Add a database context (using InMemory database for testing)
            services.AddDbContext<OrderDbContext>(options =>
            {
                options.UseInMemoryDatabase("InMemoryDbForTesting");
            });

            var sp = services.BuildServiceProvider();

            using (var scope = sp.CreateScope())
            {
                var scopedServices = scope.ServiceProvider;
                var db = scopedServices.GetRequiredService<OrderDbContext>();

                // Ensure the database is created
                db.Database.EnsureCreated();

                // Seed the database with test data
                SeedTestData(db);
            }
        });
    }

    private static void SeedTestData(OrderDbContext db)
    {
        // Clear existing data
        db.Orders.RemoveRange(db.Orders);
        db.SaveChanges();

        var order1 = new Order
        {
            Id = 1,
            CreatedUtc = DateTime.UtcNow.AddDays(-5),
            CustomerId = "guest",
            Status = "Paid",
            Total = 99.98m,
            Lines = new List<OrderLine>
            {
                new OrderLine
                {
                    Id = 1,
                    OrderId = 1,
                    Sku = "SKU-0001",
                    Name = "Apparel Item 1",
                    UnitPrice = 49.99m,
                    Quantity = 2
                }
            }
        };

        var order2 = new Order
        {
            Id = 2,
            CreatedUtc = DateTime.UtcNow.AddDays(-2),
            CustomerId = "customer123",
            Status = "Shipped",
            Total = 149.99m,
            Lines = new List<OrderLine>
            {
                new OrderLine
                {
                    Id = 2,
                    OrderId = 2,
                    Sku = "SKU-0002",
                    Name = "Electronics Item 2",
                    UnitPrice = 149.99m,
                    Quantity = 1
                }
            }
        };

        db.Orders.AddRange(order1, order2);
        db.SaveChanges();
    }
}

public class OrderApiTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public OrderApiTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task GetOrders_ReturnsAllOrders_WhenNoFilterProvided()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync("/api/orders");

        // Assert
        response.EnsureSuccessStatusCode();
        var orders = await response.Content.ReadFromJsonAsync<List<Order>>();
        
        Assert.NotNull(orders);
        Assert.Equal(2, orders.Count);
    }

    [Fact]
    public async Task GetOrders_ReturnsFilteredOrders_WhenCustomerIdProvided()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync("/api/orders?customerId=guest");

        // Assert
        response.EnsureSuccessStatusCode();
        var orders = await response.Content.ReadFromJsonAsync<List<Order>>();
        
        Assert.NotNull(orders);
        Assert.Single(orders);
        Assert.Equal("guest", orders[0].CustomerId);
    }

    [Fact]
    public async Task GetOrderById_ReturnsOrder_WhenOrderExists()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync("/api/orders/1");

        // Assert
        response.EnsureSuccessStatusCode();
        var order = await response.Content.ReadFromJsonAsync<Order>();
        
        Assert.NotNull(order);
        Assert.Equal(1, order.Id);
        Assert.Equal("guest", order.CustomerId);
        Assert.Equal("Paid", order.Status);
        Assert.Equal(99.98m, order.Total);
        Assert.NotNull(order.Lines);
        Assert.Single(order.Lines);
        Assert.Equal("SKU-0001", order.Lines[0].Sku);
    }

    [Fact]
    public async Task GetOrderById_Returns404_WhenOrderDoesNotExist()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync("/api/orders/999");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task CreateOrder_CreatesNewOrder_WithValidData()
    {
        // Arrange
        var client = _factory.CreateClient();
        var newOrder = new Order
        {
            CustomerId = "newcustomer",
            Status = "Created",
            Total = 199.99m,
            Lines = new List<OrderLine>
            {
                new OrderLine
                {
                    Sku = "SKU-0003",
                    Name = "New Product",
                    UnitPrice = 199.99m,
                    Quantity = 1
                }
            }
        };

        // Act
        var response = await client.PostAsJsonAsync("/api/orders", newOrder);

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var createdOrder = await response.Content.ReadFromJsonAsync<Order>();
        
        Assert.NotNull(createdOrder);
        Assert.True(createdOrder.Id > 0);
        Assert.Equal("newcustomer", createdOrder.CustomerId);
        Assert.Equal("Created", createdOrder.Status);
        Assert.Equal(199.99m, createdOrder.Total);
        
        // Verify the Location header
        Assert.NotNull(response.Headers.Location);
        Assert.Contains($"/api/orders/{createdOrder.Id}", response.Headers.Location.ToString());
    }

    [Fact]
    public async Task HealthCheck_ReturnsHealthy()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync("/health");

        // Assert
        response.EnsureSuccessStatusCode();
        var content = await response.Content.ReadAsStringAsync();
        Assert.Equal("Healthy", content);
    }

    [Fact]
    public async Task GetOrders_ReturnsOrdersDescendingByCreatedDate()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync("/api/orders");

        // Assert
        response.EnsureSuccessStatusCode();
        var orders = await response.Content.ReadFromJsonAsync<List<Order>>();
        
        Assert.NotNull(orders);
        Assert.True(orders.Count >= 2);
        
        // Verify descending order
        for (int i = 0; i < orders.Count - 1; i++)
        {
            Assert.True(orders[i].CreatedUtc >= orders[i + 1].CreatedUtc);
        }
    }

    [Fact]
    public async Task GetOrderById_IncludesOrderLines()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync("/api/orders/2");

        // Assert
        response.EnsureSuccessStatusCode();
        var order = await response.Content.ReadFromJsonAsync<Order>();
        
        Assert.NotNull(order);
        Assert.NotNull(order.Lines);
        Assert.NotEmpty(order.Lines);
        
        var line = order.Lines[0];
        Assert.Equal("SKU-0002", line.Sku);
        Assert.Equal("Electronics Item 2", line.Name);
        Assert.Equal(149.99m, line.UnitPrice);
        Assert.Equal(1, line.Quantity);
    }
}


