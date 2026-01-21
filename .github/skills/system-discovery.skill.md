---
name: system-discovery
version: 1.0
purpose: Build an accurate, evidence-based map of the current system (structure, flows, dependencies, data).
inputs:
  - repository source code
  - configuration files (appsettings, env templates)
  - docs (if present)
outputs:
  - system inventory (components/modules)
  - domain boundary hypotheses (validated against code)
  - key execution flows (request → service → data)
  - dependency map (internal + external)
guardrails:
  - be strictly factual; cite file paths and symbols
  - do not infer future-state architecture
  - do not refactor production code
definition_of_done:
  - a new engineer can understand how the app runs and where core behaviours live
---

## Execution Procedure

### Step 1 — Repo Orientation
- Identify entry points (e.g., Program.cs / Startup.cs).
- Identify project boundaries (csproj/sln), folders, and key layers (Models, Services, Data, Pages/Controllers).
- Capture runtime assumptions (ports, environment variables, local dependencies).

### Step 2 — Domain Boundary Mapping
- Identify core domains (Products, Cart, Orders, Checkout) by:
  - folder structure
  - model classes
  - service classes
  - pages/controllers
  - DB sets/migrations
- Produce a domain map with:
  - “owns data”
  - “owns business rules”
  - “owns HTTP surface area”
  - dependencies between domains

### Step 3 — Flow Tracing (Happy Paths)
Trace at least 2–3 real flows end-to-end:
- UI/request entry → handler/controller → service → data access → response
- Record file paths + key methods involved.

### Step 4 — Data Model & Persistence
- Identify the DbContext and DbSets.
- Identify migrations and the shape of the schema.
- Note any coupling patterns (shared context across domains, direct entity access from UI, etc.).

### Step 5 — External Dependencies & Integrations
- Identify:
  - third-party packages
  - payment gateways / external service clients
  - messaging (if any)
  - storage dependencies
- Summarise operational dependencies and failure modes.

### Step 6 — Produce Evidence-Based Outputs
Outputs must include:
- “System Inventory” list
- “Domain Map” (table format is fine)
- “Key Flows” with file paths
- “Coupling Hotspots” (where changes are risky)

## Output Format Standard
When referencing code, always include:
- file path
- class / method name
- short description of responsibility

Example:
- `Services/CheckoutService.cs::ProcessCheckoutAsync` — orchestrates payment + order creation.
