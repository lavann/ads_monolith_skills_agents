using Microsoft.EntityFrameworkCore;
using OrderService.Data;
using OrderService.Models;

var builder = WebApplication.CreateBuilder(args);

// Add database context with SQL Server or In-Memory for testing
if (builder.Environment.EnvironmentName == "Testing")
{
    builder.Services.AddDbContext<OrderDbContext>(options =>
        options.UseInMemoryDatabase("OrderServiceTestDb"));
}
else
{
    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
    if (string.IsNullOrEmpty(connectionString))
    {
        throw new InvalidOperationException(
            "Database connection string 'DefaultConnection' is not configured. " +
            "Please set the ConnectionStrings__DefaultConnection environment variable or configure it in appsettings.json.");
    }
    
    builder.Services.AddDbContext<OrderDbContext>(options =>
        options.UseSqlServer(connectionString));
}

// Configure JSON options to handle reference cycles
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles;
});

// Add health checks
builder.Services.AddHealthChecks()
    .AddDbContextCheck<OrderDbContext>();

var app = builder.Build();

// Health check endpoint
app.MapHealthChecks("/health");

// Order API Endpoints
app.MapGet("/api/orders", async (OrderDbContext db, string? customerId) =>
{
    var query = db.Orders.Include(o => o.Lines).AsQueryable();
    
    // Filter by customer if provided
    if (!string.IsNullOrEmpty(customerId))
    {
        query = query.Where(o => o.CustomerId == customerId);
    }
    
    var orders = await query
        .OrderByDescending(o => o.CreatedUtc)
        .ToListAsync();
    
    return Results.Ok(orders);
})
.WithName("GetOrders")
.WithTags("Orders")
.Produces<List<Order>>(StatusCodes.Status200OK);

app.MapGet("/api/orders/{id:int}", async (int id, OrderDbContext db) =>
{
    var order = await db.Orders
        .Include(o => o.Lines)
        .FirstOrDefaultAsync(o => o.Id == id);
    
    return order is null ? Results.NotFound() : Results.Ok(order);
})
.WithName("GetOrderById")
.WithTags("Orders")
.Produces<Order>(StatusCodes.Status200OK)
.Produces(StatusCodes.Status404NotFound);

app.MapPost("/api/orders", async (Order order, OrderDbContext db) =>
{
    db.Orders.Add(order);
    await db.SaveChangesAsync();
    
    return Results.Created($"/api/orders/{order.Id}", order);
})
.WithName("CreateOrder")
.WithTags("Orders")
.Produces<Order>(StatusCodes.Status201Created);

app.Run();

// Make Program class visible to test project for WebApplicationFactory
public partial class Program { }

