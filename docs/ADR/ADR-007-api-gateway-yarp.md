# ADR-007: API Gateway Technology Selection - YARP

**Status**: Proposed  
**Date**: 2025-01-21  
**Context**: Modernisation Planning

## Context

As we extract services from the monolith, we need a mechanism to:
- Route incoming requests to appropriate backend services
- Provide a single entry point for frontend clients
- Enable gradual migration (some requests to monolith, some to services)
- Support future requirements (authentication, rate limiting, logging)

An API Gateway serves as the single entry point for all client requests, routing them to the appropriate backend service.

## Decision

Use **YARP (Yet Another Reverse Proxy)** as the API Gateway for Phases 1-3, with an option to upgrade to Azure API Management for Phase 4+ if advanced features are needed.

**YARP**: Open-source .NET reverse proxy library from Microsoft
- Repository: https://github.com/microsoft/reverse-proxy
- NuGet: `Yarp.ReverseProxy`
- License: MIT

## Rationale

### Why YARP?

**1. .NET Native**
- ✅ Built on ASP.NET Core (familiar to team)
- ✅ Same tech stack as our services (.NET 9)
- ✅ Excellent performance (built on Kestrel)

**2. Configuration-Driven**
- ✅ JSON-based configuration (no custom routing code)
- ✅ Dynamic configuration updates (no restart required)
- ✅ Easy to version control and deploy

**3. Free and Open Source**
- ✅ No licensing costs
- ✅ MIT license (permissive)
- ✅ Backed by Microsoft (long-term support)

**4. Extensible**
- ✅ Middleware pipeline (add auth, logging, rate limiting)
- ✅ Custom transformations (modify requests/responses)
- ✅ Health checks and load balancing

**5. Proven in Production**
- ✅ Used by Microsoft internally (Azure services)
- ✅ Active development and community

**6. Ideal for Strangler Fig Pattern**
- ✅ Gradual routing migration (monolith → service)
- ✅ Weighted routing (canary deployments)
- ✅ Header-based routing (feature flags)

### Why Not Alternatives?

**Azure API Management (APIM)**:
- ✅ **Pros**: Enterprise features (analytics, rate limiting, monetization)
- ❌ **Cons**: High cost ($200+/month), cloud lock-in, overkill for our needs
- **Decision**: Consider for Phase 4+ if we need advanced features

**Ocelot**:
- ✅ **Pros**: .NET native, mature
- ❌ **Cons**: No longer actively maintained, superseded by YARP
- **Decision**: Rejected (use YARP instead)

**NGINX**:
- ✅ **Pros**: Battle-tested, high performance, widely used
- ❌ **Cons**: Configuration complexity, not .NET native, requires separate deployment
- **Decision**: Good alternative, but YARP preferred for .NET ecosystem fit

**Kong**:
- ✅ **Pros**: Rich plugin ecosystem, cloud-native
- ❌ **Cons**: Lua-based plugins (learning curve), separate deployment
- **Decision**: Overkill for our needs

**Envoy**:
- ✅ **Pros**: CNCF project, used by Istio
- ❌ **Cons**: Complex configuration, C++-based, steep learning curve
- **Decision**: Too complex for our team

## Consequences

### Positive

- ✅ **Fast implementation**: .NET team can configure YARP quickly
- ✅ **No new technology**: Same stack as services (ASP.NET Core)
- ✅ **Low operational complexity**: Deployed as Docker container like services
- ✅ **Flexible routing**: Easy to change routes during migration
- ✅ **Cost**: Free (open source)

### Negative

- ❌ **Limited analytics**: No built-in dashboards (need separate APM)
- ❌ **Limited rate limiting**: Basic implementation (need custom middleware)
- ❌ **No GUI**: Configuration via JSON files (no web UI)
- ❌ **Less mature**: Newer than NGINX/Kong (fewer production deployments)

### Risks

| Risk | Likelihood | Impact | Mitigation |
|------|-----------|--------|------------|
| **YARP bug impacts routing** | Low | High | Extensive testing, canary deployments, rollback to monolith |
| **Performance bottleneck** | Low | Medium | Load testing, horizontal scaling, caching |
| **Configuration errors** | Medium | Medium | Schema validation, automated testing, peer review |
| **Limited feature set** | Low | Low | Custom middleware, or upgrade to APIM later |

## Implementation

### Project Structure

```
src/ApiGateway/
├── ApiGateway.csproj
├── Program.cs
├── appsettings.json          # YARP routes configuration
├── appsettings.Development.json
├── Middleware/
│   ├── AuthenticationMiddleware.cs   (Phase 3+)
│   ├── RateLimitingMiddleware.cs     (Phase 3+)
│   └── LoggingMiddleware.cs
└── Dockerfile
```

### YARP Configuration

**appsettings.json**:
```json
{
  "ReverseProxy": {
    "Routes": {
      "products-route": {
        "ClusterId": "product-service",
        "Match": {
          "Path": "/api/products/{**catch-all}"
        }
      },
      "orders-route": {
        "ClusterId": "order-service",
        "Match": {
          "Path": "/api/orders/{**catch-all}"
        }
      },
      "carts-route": {
        "ClusterId": "cart-service",
        "Match": {
          "Path": "/api/carts/{**catch-all}"
        }
      },
      "checkout-route": {
        "ClusterId": "checkout-service",
        "Match": {
          "Path": "/api/checkout"
        },
        "Transforms": [
          {
            "RequestHeader": "X-Gateway-Source",
            "Set": "YARP"
          }
        ]
      },
      "fallback-route": {
        "ClusterId": "monolith",
        "Match": {
          "Path": "{**catch-all}"
        },
        "Order": 999
      }
    },
    "Clusters": {
      "product-service": {
        "Destinations": {
          "destination1": {
            "Address": "http://product-service:8080"
          }
        },
        "HealthCheck": {
          "Active": {
            "Enabled": true,
            "Interval": "00:00:10",
            "Timeout": "00:00:05",
            "Policy": "ConsecutiveFailures",
            "Path": "/health"
          }
        }
      },
      "order-service": {
        "Destinations": {
          "destination1": {
            "Address": "http://order-service:8080"
          }
        },
        "HealthCheck": {
          "Active": {
            "Enabled": true,
            "Interval": "00:00:10",
            "Timeout": "00:00:05",
            "Policy": "ConsecutiveFailures",
            "Path": "/health"
          }
        }
      },
      "cart-service": {
        "Destinations": {
          "destination1": {
            "Address": "http://cart-service:8080"
          }
        }
      },
      "checkout-service": {
        "Destinations": {
          "destination1": {
            "Address": "http://checkout-service:8080"
          }
        }
      },
      "monolith": {
        "Destinations": {
          "destination1": {
            "Address": "http://monolith:8080"
          }
        }
      }
    }
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Yarp": "Debug"
    }
  }
}
```

### Program.cs

```csharp
using Yarp.ReverseProxy;

var builder = WebApplication.CreateBuilder(args);

// Add YARP reverse proxy
builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

// Add health checks
builder.Services.AddHealthChecks();

var app = builder.Build();

// Map health check endpoint
app.MapHealthChecks("/health");

// Map YARP reverse proxy
app.MapReverseProxy();

app.Run();
```

### Dockerfile

```dockerfile
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY ["ApiGateway.csproj", "./"]
RUN dotnet restore
COPY . .
RUN dotnet build -c Release -o /app/build

FROM build AS publish
RUN dotnet publish -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "ApiGateway.dll"]
```

## Migration Workflow

### Phase 0-1: Monolith Only

All routes go to monolith:
```json
{
  "Routes": {
    "catch-all": {
      "ClusterId": "monolith",
      "Match": {
        "Path": "{**catch-all}"
      }
    }
  }
}
```

### Phase 1: Order Service Extracted

Orders routed to service, everything else to monolith:
```json
{
  "Routes": {
    "orders-route": {
      "ClusterId": "order-service",
      "Match": {
        "Path": "/api/orders/{**catch-all}"
      }
    },
    "fallback": {
      "ClusterId": "monolith",
      "Match": {
        "Path": "{**catch-all}"
      },
      "Order": 999
    }
  }
}
```

### Phase 2: Product Service Extracted

Products and Orders routed to services:
```json
{
  "Routes": {
    "products-route": {
      "ClusterId": "product-service",
      "Match": {
        "Path": "/api/products/{**catch-all}"
      }
    },
    "orders-route": {
      "ClusterId": "order-service",
      "Match": {
        "Path": "/api/orders/{**catch-all}"
      }
    },
    "fallback": {
      "ClusterId": "monolith",
      "Match": {
        "Path": "{**catch-all}"
      },
      "Order": 999
    }
  }
}
```

### Phase 3: All Services Extracted

All API routes go to services, UI to monolith:
```json
{
  "Routes": {
    "api-routes": {
      "ClusterId": "appropriate-service",
      "Match": {
        "Path": "/api/{**catch-all}"
      }
    },
    "ui-fallback": {
      "ClusterId": "web-frontend",
      "Match": {
        "Path": "{**catch-all}"
      },
      "Order": 999
    }
  }
}
```

## Advanced Features (Phase 3+)

### 1. Authentication Middleware

```csharp
app.Use(async (context, next) =>
{
    var token = context.Request.Headers["Authorization"].FirstOrDefault();
    
    if (string.IsNullOrEmpty(token))
    {
        context.Response.StatusCode = 401;
        await context.Response.WriteAsync("Unauthorized");
        return;
    }
    
    // Validate JWT token
    // ...
    
    await next();
});
```

### 2. Rate Limiting

```csharp
builder.Services.AddRateLimiter(options =>
{
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: context.Request.Headers["X-Client-Id"].ToString(),
            factory: partition => new FixedWindowRateLimiterOptions
            {
                AutoReplenishment = true,
                PermitLimit = 100,
                Window = TimeSpan.FromMinutes(1)
            }));
});

app.UseRateLimiter();
```

### 3. Request/Response Logging

```csharp
app.Use(async (context, next) =>
{
    var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();
    
    logger.LogInformation("Request: {Method} {Path}", 
        context.Request.Method, context.Request.Path);
    
    await next();
    
    logger.LogInformation("Response: {StatusCode}", 
        context.Response.StatusCode);
});
```

### 4. Header Transformation

```json
{
  "Transforms": [
    {
      "RequestHeader": "X-Correlation-Id",
      "Set": "{{RequestId}}"
    },
    {
      "ResponseHeader": "X-Gateway-Version",
      "Set": "1.0"
    }
  ]
}
```

### 5. Load Balancing (Multiple Instances)

```json
{
  "Clusters": {
    "product-service": {
      "Destinations": {
        "destination1": {
          "Address": "http://product-service-1:8080"
        },
        "destination2": {
          "Address": "http://product-service-2:8080"
        }
      },
      "LoadBalancingPolicy": "RoundRobin"
    }
  }
}
```

## Validation

### Success Criteria

- ✅ API Gateway routes requests to correct services (verified via logs)
- ✅ Health checks detect unhealthy services (verified via tests)
- ✅ Configuration updates apply without restart (hot reload)
- ✅ Response time overhead < 10ms (measured via APM)
- ✅ Gateway scales horizontally (multiple replicas in Kubernetes)

### Testing

**Unit Tests**: Configuration validation
```csharp
[Fact]
public void Configuration_IsValid()
{
    var config = new ConfigurationBuilder()
        .AddJsonFile("appsettings.json")
        .Build();
    
    var routes = config.GetSection("ReverseProxy:Routes").GetChildren();
    Assert.NotEmpty(routes);
}
```

**Integration Tests**: End-to-end routing
```csharp
[Fact]
public async Task Gateway_RoutesOrdersToOrderService()
{
    var client = _factory.CreateClient();
    var response = await client.GetAsync("/api/orders/1");
    
    Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    // Verify response came from Order Service (via header or content)
}
```

**Load Tests**: Performance under load
```bash
# Using Apache Bench
ab -n 10000 -c 100 http://localhost:5000/api/products

# Target: 95th percentile < 50ms
```

## References

- [YARP Documentation](https://microsoft.github.io/reverse-proxy/)
- [YARP GitHub Repository](https://github.com/microsoft/reverse-proxy)
- [Comparing API Gateway Options](https://learn.microsoft.com/en-us/azure/architecture/microservices/design/gateway)
- [API Gateway Pattern - Martin Fowler](https://microservices.io/patterns/apigateway.html)

## Related ADRs

- ADR-005: Service Decomposition Strategy
- ADR-008: Saga Pattern for Distributed Transactions (Checkout Service routing)
