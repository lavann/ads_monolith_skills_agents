using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using RetailMonolith.Data;

namespace RetailMonolith.Tests.E2E
{
    /// <summary>
    /// E2E tests for application startup.
    /// Validates that the application can start successfully with test dependencies.
    /// </summary>
    public class HealthCheckTests : IClassFixture<WebApplicationFactory<Program>>
    {
        private readonly WebApplicationFactory<Program> _factory;

        public HealthCheckTests(WebApplicationFactory<Program> factory)
        {
            _factory = factory.WithWebHostBuilder(builder =>
            {
                builder.ConfigureServices(services =>
                {
                    // Remove the existing DbContext registration
                    var descriptor = services.SingleOrDefault(
                        d => d.ServiceType == typeof(DbContextOptions<AppDbContext>));
                    
                    if (descriptor != null)
                    {
                        services.Remove(descriptor);
                    }

                    // Add in-memory database for testing
                    services.AddDbContext<AppDbContext>(options =>
                    {
                        options.UseInMemoryDatabase($"E2E_HealthCheck_Test_{Guid.NewGuid()}");
                    });
                });

                // Skip auto-migration in tests
                builder.UseSetting("SkipAutoMigration", "true");
            });
        }

        [Fact]
        public async Task Application_Starts_Successfully()
        {
            // Arrange
            var client = _factory.CreateClient();

            // Act - Application should start and respond to requests
            // We don't care about the response status, just that it responds
            try
            {
                var response = await client.GetAsync("/");
                // Assert - Application started successfully (any HTTP response means it's running)
                Assert.NotNull(response);
            }
            catch (Exception ex)
            {
                Assert.Fail($"Application failed to start: {ex.Message}");
            }
        }

        [Fact]
        public void Database_Context_Can_Be_Resolved()
        {
            // Arrange & Act - Try to resolve DbContext from DI
            using var scope = _factory.Services.CreateScope();
            var context = scope.ServiceProvider.GetService<AppDbContext>();

            // Assert - Context should be available
            Assert.NotNull(context);
        }
    }
}
