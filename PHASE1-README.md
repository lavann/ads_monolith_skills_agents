# Phase 1: Order Service Extraction - Quick Start

This directory contains the Phase 1 implementation of the Intelligent Migration Plan: extracting the Order Service from the monolith.

## Quick Start

### Run All Services with Docker Compose

```bash
# Build and start all services
docker-compose up -d

# Check service health
docker-compose ps

# View logs
docker-compose logs -f
```

### Access Services

- **API Gateway** (main entry point): http://localhost:5002
- **Monolith** (direct access): http://localhost:5000  
- **Order Service** (direct access): http://localhost:5001
- **SQL Server**: localhost:1433

### Test the Order Service via Gateway

```bash
# Get all orders
curl http://localhost:5002/api/orders

# Get specific order
curl http://localhost:5002/api/orders/1

# Health check
curl http://localhost:5002/health
```

### Run Tests

```bash
# Run all tests
dotnet test

# Run only OrderService tests
dotnet test OrderService.Tests/

# Run only Monolith tests
dotnet test RetailMonolith.Tests/
```

## Architecture

```
                                    ┌──────────────┐
                                    │  API Gateway │
                                    │   (YARP)     │
                                    └──────┬───────┘
                                           │
                        ┌──────────────────┴────────────────┐
                        │                                   │
                  /api/orders/*                        /**  │
                        │                                   │
                  ┌─────▼─────┐                     ┌───────▼───────┐
                  │   Order   │                     │     Retail    │
                  │  Service  │                     │   Monolith    │
                  └─────┬─────┘                     └───────┬───────┘
                        │                                   │
                        │         ┌────────────────┐        │
                        └─────────►  SQL Server    ◄────────┘
                                  │ (Shared DB)    │
                                  └────────────────┘
```

## Project Structure

```
/
├── ApiGateway/              # YARP reverse proxy
├── OrderService/            # Extracted Order microservice
├── OrderService.Tests/      # Order service tests
├── RetailMonolith/          # Original monolith application
├── RetailMonolith.Tests/    # Monolith tests
├── docker-compose.yml       # Multi-service orchestration
└── docs/
    └── Phase-1-Implementation.md  # Comprehensive documentation
```

## Key Features

✅ **Strangler Fig Pattern**: OrderService shares database with monolith  
✅ **Read-Only Extraction**: Order retrieval endpoints migrated first  
✅ **API Gateway Routing**: Transparent routing to appropriate service  
✅ **Comprehensive Tests**: 29 tests (21 monolith + 8 OrderService) all passing  
✅ **Container-Ready**: Full Docker Compose setup for local development  
✅ **Easy Rollback**: < 10 minute rollback via routing change only  

## Documentation

- **[Phase-1-Implementation.md](./docs/Phase-1-Implementation.md)** - Complete implementation guide
- **[Intelligent-Migration-Plan.md](./docs/Intelligent-Migration-Plan.md)** - Overall migration strategy
- **[Target-Architecture.md](./docs/Target-Architecture.md)** - Target architecture design

## Common Tasks

### Stop All Services

```bash
docker-compose stop
```

### Rebuild After Code Changes

```bash
docker-compose build
docker-compose up -d
```

### Clean Up Everything

```bash
# WARNING: This deletes all data
docker-compose down -v
```

### Run Service Individually

```bash
# Just the OrderService
cd OrderService
dotnet run
```

## Next Steps

1. ✅ Phase 1 Complete: Order Service Extracted
2. ⏳ Phase 2: Extract Product Service with Caching
3. ⏳ Phase 3: Extract Inventory, Cart, Checkout (Saga Pattern)
4. ⏳ Phase 4: Database Decomposition
5. ⏳ Phase 5: Frontend Modernization (Optional)

## Support

For questions or issues:
- See [Phase-1-Implementation.md](./docs/Phase-1-Implementation.md) for detailed documentation
- Check [Rollback Procedure](./docs/Phase-1-Implementation.md#rollback-procedure) if issues arise
- Review [Known Limitations](./docs/Phase-1-Implementation.md#known-limitations--future-work)

---

**Status**: Phase 1 Complete ✅  
**Last Updated**: 2026-01-23
