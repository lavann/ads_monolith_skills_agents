---
name: incremental-refactoring
version: 1.0
purpose: Implement one migration slice safely: small, reviewable, reversible, and fully tested.
inputs:
  - /docs/Migration-Plan.md (slice definition)
  - /docs/Target-Architecture.md
  - /docs/Test-Strategy.md
outputs:
  - a PR implementing exactly one slice
  - updated tests
  - updated docs/ADRs when behaviour changes
guardrails:
  - one slice only
  - keep PR small and reviewable
  - preserve existing behaviour
  - all tests must pass
definition_of_done:
  - slice acceptance criteria met, CI green, rollback plan clear
---

## Procedure

### Step 1 — Confirm Slice Contract
Extract from Migration Plan:
- scope
- acceptance criteria
- rollback strategy
- boundaries (what is explicitly out of scope)

### Step 2 — Prepare the Smallest Working Change
Prefer:
- scaffolding first (project/service skeleton)
- then routing/integration
- then clean-up

### Step 3 — Preserve Behaviour
- keep existing API surface stable where possible
- maintain backward compatible routes
- avoid changing data semantics in early slices

### Step 4 — Validate Continuously
- run unit tests
- run integration tests
- add new tests for the slice
- keep PR commits structured (setup → change → tests → docs)

### Step 5 — Update Operational Artefacts
Update:
- Runbook (how to run new service)
- ADRs (decisions introduced)
- Migration Plan (mark slice complete + note learnings)

### Step 6 — Rollback Readiness
Ensure rollback is possible by:
- toggling routing back
- running monolith-only path
- avoiding irreversible schema changes early

## PR Quality Bar
- clear description of what changed and why
- explicit “how to test”
- explicit “risk / rollback”
