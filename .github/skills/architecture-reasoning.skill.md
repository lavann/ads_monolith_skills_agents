---
name: architecture-reasoning
version: 1.0
purpose: Propose a target architecture and an incremental migration plan that is achievable and reversible.
inputs:
  - /docs/HLD.md
  - /docs/LLD.md
  - /docs/ADR/*
  - constraints (hosting, security, time)
outputs:
  - target architecture (service boundaries + deployment model)
  - migration plan (phased, strangler-style)
  - ADRs for new decisions
guardrails:
  - no big-bang rewrite
  - first slice must be minimal and demoable
  - explicitly call out risks and rollback paths
definition_of_done:
  - a team can implement Slice 0 and Slice 1 without guessing
---

## Reasoning Procedure

### Step 1 — Establish Constraints
Confirm:
- hosting target (containers, App Service, or Azure Container Apps (ACA))
- governance posture (PR gates, CI, reviews)
- data constraints (shared DB initially OK)
- non-functional requirements (security, observability baseline)

### Step 2 — Identify Natural Service Boundaries
Use the domain map and coupling hotspots to propose:
- candidate services
- what stays in the monolith initially
- dependency direction rules (avoid cyclical service calls)

### Step 3 — Choose a First Slice
Pick a first slice that is:
- low-risk
- verifiable via tests
- small enough for one PR cycle

Preferred first slices:
- stabilisation (CI + container + runbook hardening)
- read-only domain extraction (e.g., Orders Query)

Justify choice.

### Step 4 — Define Target Deployment Model
Specify:
- container boundaries
- routing strategy (reverse proxy, API gateway later; simple internal routing now)
- configuration strategy (env vars, secrets handling)
- observability hooks (health, logging, basic metrics)

### Step 5 — Migration Plan (Strangler)
Create phases:
- Slice 0: stabilise and baseline tests
- Slice 1: first extraction
- Slice 2: routing hardening + contract tests
- Slice 3: data decoupling (only when necessary)
- Slice N: repeat

Each phase must include:
- entry criteria
- change set
- exit criteria
- rollback plan

### Step 6 — Document Decisions as ADRs
For each significant decision, write an ADR:
- decision
- context
- options considered
- consequences

## Output Format Standard

### Target Architecture must include:
- service list (name + responsibility)
- service-to-service communication approach
- data access approach (explicitly state “shared DB initially” if chosen)
- deployment diagram (textual is fine)

### Migration Plan must include:
- a table of phases with acceptance criteria
- a clear “next action” for Implementation Agent
