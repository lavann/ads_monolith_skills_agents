---
name: incremental-refactoring
version: 1.1
purpose: Implement one approved migration slice safely in a factory-compatible way small, reviewable, reversible, and fully tested.
inputs:
  - /docs/Migration-Plan.md
  - /docs/Target-Architecture.md
  - /docs/Test-Strategy.md
  - /docs/Intelligent-Migration-Plan.md
  - /docs/Risk-and-Governance.md
  - /docs/Intelligent-Team-Model.md
outputs:
  - a pull request implementing exactly one slice
  - updated/added tests
  - updated runbook/ADRs where required
guardrails:
  - one slice only
  - preserve behaviour unless explicitly approved
  - keep PR reviewable and reversible
  - conform to risk posture and phase gates defined by the intelligent migration programme
definition_of_done:
  - slice acceptance criteria met, CI green, rollback remains valid, and governance constraints are respected
---

## Procedure

### Step 0 — Gate Check (must pass before changes)
Confirm the following are present and consistent:
- Slice definition exists in `/docs/Migration-Plan.md`
- Target architecture expectations exist in `/docs/Target-Architecture.md`
- Test baseline exists in `/docs/Test-Strategy.md`
- Programme constraints exist in:
  - `/docs/Intelligent-Migration-Plan.md`
  - `/docs/Risk-and-Governance.md`
  - `/docs/Intelligent-Team-Model.md`

If any are missing or contradictory:
- stop implementation
- raise a PR comment explaining the gap
- request a planning update

### Step 1 — Extract the Slice Contract
From the Migration Plan, capture explicitly in the PR description:
- scope (what is in / out)
- acceptance criteria
- dependencies
- rollback approach

Also capture from the Intelligent Migration Plan:
- phase gate this slice belongs to (e.g., Slice 0 stabilisation, Slice 1 extraction)
- risk posture constraints that must not be violated (e.g., no DB split yet, no auth changes, no runtime changes)

### Step 2 — Implement the Smallest Safe Change
Prefer incremental structure:
1) scaffold / plumbing
2) functional change
3) tests
4) docs/run steps

Keep the blast radius minimal:
- avoid broad refactors
- avoid large renames and formatting churn
- avoid changing multiple domains at once

### Step 3 — Preserve Behaviour
- keep existing API/routes stable unless migration plan explicitly changes them
- maintain backward compatibility where feasible
- avoid irreversible schema changes in early slices unless explicitly approved

### Step 4 — Validate Continuously
- run unit tests
- run integration tests
- add slice-specific tests
- ensure CI runs cleanly

If tests are missing or unreliable, prioritise fixing tests before adding features.

### Step 5 — Update Operational Artefacts
Update as required:
- `/docs/Runbook.md` (how to run/build/test after the slice)
- `/docs/ADR/*` if a new decision is introduced
- `/docs/Migration-Plan.md` (mark slice complete + note learnings if instructed)

### Step 6 — Rollback Integrity
Ensure rollback remains possible and documented:
- routing can be toggled back
- monolith-only path can operate
- no irreversible changes without explicit approval

## PR Requirements (non-negotiable)
Every PR must include in the description:
- “Slice implemented” (name)
- “How tested” (commands + CI evidence)
- “Risk / rollback” (one clear paragraph)
- “Programme alignment” (one line confirming alignment to Intelligent Migration Plan and Risk/Governance)

## Quality Bar
- Small PRs that can be reviewed quickly
- Tests green
- Behaviour preserved
- Governance constraints respected
- Rollback remains valid and documented
- CI green
- Human review completed
- No deviations from the Intelligent Migration Plan without explicit note in PR description
## Definition of Done
- Slice acceptance criteria (from Migration Plan) are met
- All existing and new tests pass
- Application builds and runs locally
- Rollback approach remains valid and documented
- No deviation from agreed risk posture or scope
- All work delivered via a pull request
- Green CI is mandatory
- Human review is required before merge
- Any deviation from the Intelligent Migration Plan is surfaced explicitly in the PR description
