# Modernisation Plan - Quick Start Guide

**Status**: Ready for Review  
**Date**: 2025-01-21  
**Purpose**: Guide stakeholders through the Retail Monolith modernisation documentation

## Overview

This directory contains a comprehensive plan to modernise the Retail Monolith from a single ASP.NET Core application into a containerised microservices architecture using the **strangler fig pattern**.

**Total Estimated Duration**: 8-12 weeks  
**Migration Approach**: Incremental, reversible, behaviour-preserving  
**Risk Level**: Low to Medium (increases gradually)

## Documentation Structure

### 📋 Core Planning Documents

1. **[Target-Architecture.md](./Target-Architecture.md)** (22 KB)
   - Proposed service boundaries (5 microservices)
   - Container deployment model (Docker/Kubernetes)
   - API Gateway approach (YARP)
   - Data access strategy (shared DB → database per service)
   - Technology stack and configuration management

2. **[Migration-Plan.md](./Migration-Plan.md)** (42 KB)
   - **Phase 0**: Foundation & containerisation (1-2 weeks)
   - **Phase 1**: Extract Order Service - **FIRST SLICE** (1-2 weeks)
   - **Phase 2**: Extract Product Service (1-2 weeks)
   - **Phase 3**: Extract Cart, Inventory, Checkout Services (2-3 weeks)
   - **Phase 4**: Database decomposition (2-3 weeks)
   - **Phase 5**: Frontend modernisation (optional, 2-4 weeks)
   - Detailed tasks, success criteria, and rollback plans per phase

### 📜 Architecture Decision Records (ADRs)

#### Existing ADRs (Current State)
- **[ADR-001](./ADR/ADR-001-monolithic-architecture.md)**: Monolithic architecture with shared database
- **[ADR-002](./ADR/ADR-002-auto-migration-startup.md)**: Auto-migration on startup (unsafe for production)
- **[ADR-003](./ADR/ADR-003-hardcoded-guest-customer.md)**: Hardcoded "guest" customer ID
- **[ADR-004](./ADR/ADR-004-mock-payment-gateway.md)**: Mock payment gateway

#### New ADRs (Modernisation Strategy)
- **[ADR-005](./ADR/ADR-005-service-decomposition-strategy.md)**: Service boundaries using DDD
- **[ADR-006](./ADR/ADR-006-data-migration-strategy.md)**: Shared database first, database per service later
- **[ADR-007](./ADR/ADR-007-api-gateway-yarp.md)**: YARP as API Gateway
- **[ADR-008](./ADR/ADR-008-saga-pattern-distributed-transactions.md)**: Saga pattern for checkout orchestration
- **[ADR-009](./ADR/ADR-009-container-orchestration-platform.md)**: Docker Compose → Kubernetes

### 📖 Reference Documentation

- **[HLD.md](./HLD.md)**: High-Level Design (current monolith)
- **[LLD.md](./LLD.md)**: Low-Level Design (current monolith)
- **[Runbook.md](./Runbook.md)**: Operational procedures

## Key Decisions Summary

| Decision Area | Choice | Rationale |
|---------------|--------|-----------|
| **Migration Pattern** | Strangler Fig | Incremental, low-risk, reversible |
| **First Slice** | Order Service | Read-only, no dependencies, low risk |
| **API Gateway** | YARP | .NET native, simple, free |
| **Database Strategy** | Shared DB (Phase 1-3), Database per Service (Phase 4+) | Minimizes risk, buys time to validate boundaries |
| **Distributed Transactions** | Saga Orchestration | Clear control flow, easy to debug |
| **Orchestration** | Docker Compose (dev), Kubernetes (prod) | Gradual learning curve |

## Critical Issues Addressed

The modernisation plan fixes these critical bugs in the current monolith:

1. **Inventory-Payment Race Condition** (CheckoutService.cs:27-36)
   - **Problem**: Inventory decremented BEFORE payment charged → stock loss on payment failure
   - **Solution**: Two-phase inventory reservation (reserve → charge payment → commit)

2. **Auto-Migration on Startup** (Program.cs:27)
   - **Problem**: Unsafe for production (downtime, data loss, concurrent migrations)
   - **Solution**: Manual migrations via CI/CD, disabled in production

3. **Hardcoded "guest" Customer** (throughout codebase)
   - **Problem**: All users share cart/orders, no multi-user support
   - **Solution**: Session-based customer ID (Phase 1), JWT auth (Phase 3)

## First Slice: Order Service (Phase 1)

**Why Order Service First?**
- ✅ Read-only (low risk)
- ✅ No dependencies (isolated)
- ✅ Minimal, demoable, reversible
- ✅ Validates pattern for future extractions

**Success Criteria**:
- Order details page loads from Order Service
- Monolith creates orders via Order Service API
- Response time < 300ms
- Rollback time < 10 minutes

**Estimated Effort**: 1-2 weeks (2-3 developers)

## Reading Guide

### For Executives / Product Owners
1. Read **Migration-Plan.md** → Executive Summary and Phase Overview
2. Review **First Slice** section (Phase 1)
3. Check **Timeline and Milestones** (Week 1-12 breakdown)

### For Architects / Tech Leads
1. Read **Target-Architecture.md** → Service Boundaries and Data Strategy
2. Read all **ADRs** (especially ADR-005, ADR-006, ADR-008)
3. Review **Migration-Plan.md** → Phase 3 (Saga Implementation)

### For Developers
1. Read **Migration-Plan.md** → Phase 0 (Foundation) and Phase 1 (Order Service)
2. Review **ADR-007** (YARP configuration)
3. Review **ADR-008** (Saga pattern code samples)
4. Check **HLD.md** and **LLD.md** for current state reference

### For DevOps / SRE
1. Read **Migration-Plan.md** → Phase 0 (Containerisation)
2. Review **ADR-009** (Docker Compose → Kubernetes)
3. Review **Target-Architecture.md** → Deployment Pipeline and Observability

## Next Steps

1. **Review Meeting** (recommended attendees: Tech Lead, Product Owner, Senior Developers)
   - Review Target Architecture and Migration Plan
   - Discuss timeline and resource allocation
   - Address questions and concerns

2. **Decision Point**: Approve or request changes to modernisation plan

3. **Phase 0 Kickoff** (if approved):
   - Set up Docker environment
   - Create CI/CD pipeline
   - Fix critical bugs (inventory-payment race condition)
   - Add observability (logging, metrics)

4. **Phase 1 Execution** (after Phase 0 complete):
   - Extract Order Service
   - Deploy to staging
   - Validate success criteria
   - Deploy to production

## Risk Assessment

| Risk Level | Phases | Mitigation |
|------------|--------|------------|
| **Low** | Phase 0-2 | Read-only services, easy rollback, no data migration |
| **Medium** | Phase 3 | Distributed transactions, saga pattern, extensive testing |
| **High** | Phase 4 | Data migration, dual writes, frequent backups |

**Rollback Capability**: All phases designed with rollback in mind. Estimated rollback time: 10 minutes (config change) to 1 hour (data reconciliation).

## Success Metrics

### Technical Metrics
- ✅ Services deployable independently (target: 100%)
- ✅ Response time p95 < 500ms per service
- ✅ Error rate < 0.1% per endpoint
- ✅ Deployment frequency: 2+ per week per service (post-migration)

### Business Metrics
- ✅ Zero downtime during migration
- ✅ No functionality changes (behaviour preservation)
- ✅ Time to market reduced by 50% (post-migration)

## Questions or Concerns?

**Technical Questions**: Contact Tech Lead or review ADRs for detailed rationale

**Timeline Questions**: See Migration-Plan.md → Timeline and Milestones section

**Risk Questions**: See Migration-Plan.md → Risk Management section

**Implementation Questions**: See Migration-Plan.md → Phase-specific tasks

## References

- [Martin Fowler - Strangler Fig Application](https://martinfowler.com/bliki/StranglerFigApplication.html)
- [Sam Newman - Monolith to Microservices](https://samnewman.io/books/monolith-to-microservices/)
- [Microsoft - .NET Microservices Architecture](https://docs.microsoft.com/en-us/dotnet/architecture/microservices/)
- [Chris Richardson - Microservices Patterns](https://microservices.io/patterns/)

---

**Document Version**: 1.0  
**Last Updated**: 2025-01-21  
**Status**: Ready for Review and Approval
