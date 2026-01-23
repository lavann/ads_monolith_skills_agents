var builder = WebApplication.CreateBuilder(args);

// Add YARP reverse proxy services
builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

// Add health checks
builder.Services.AddHealthChecks();

var app = builder.Build();

// Health check endpoint
app.MapHealthChecks("/health");

// Map reverse proxy
app.MapReverseProxy();

app.Run();

