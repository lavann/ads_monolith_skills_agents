---
name: test-synthesis
version: 1.0
purpose: Create a safety net that protects current behaviour and supports incremental modernisation.
inputs:
  - /docs/HLD.md
  - /docs/LLD.md
  - /docs/Migration-Plan.md
  - source code and endpoints
outputs:
  - test strategy document
  - baseline unit tests
  - baseline integration tests
  - CI workflow to run tests on PR
guardrails:
  - tests must be deterministic
  - prefer adding tests over changing production code
  - any production change for testability must be minimal and justified
definition_of_done:
  - tests pass locally and in CI, and cover at least one critical flow end-to-end
---

## Procedure

### Step 1 — Derive Testable Contracts from HLD/LLD
Identify:
- key endpoints/pages
- core domain behaviours
- expected invariants (e.g., “cart total equals sum of items”)

### Step 2 — Build a Test Strategy Map
Create `/docs/Test-Strategy.md` that includes:
- test pyramid approach (unit/integration)
- list of critical flows and how they are tested
- explicit “known gaps”

### Step 3 — Baseline Unit Tests
Target:
- pure logic in services/helpers
- validate edge cases (nulls, empty collections, invalid inputs)
- isolate external dependencies via interfaces/mocks

### Step 4 — Baseline Integration Tests
At minimum:
- health endpoint
- one happy-path domain flow (Orders read or Products listing)
- ensure DB setup is predictable (in-memory or test container if available)

### Step 5 — Make Tests CI-Ready
Add a GitHub Actions workflow to:
- restore
- build
- test

### Step 6 — Ensure Repeatability
Document:
- how to run tests locally
- any test config requirements

## Output Quality Bar
- avoid brittle UI tests for baseline
- prioritise API/service-level integration tests
- keep tests readable and named by behaviour
