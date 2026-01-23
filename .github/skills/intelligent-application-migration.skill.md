---
name: incremental-refactoring
version: 1.1
purpose: "Implement one approved migration slice safely in a factory-compatible way small, reviewable, reversible, and fully tested."
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


## Reasoning Framework

### Step 1 — Anchor to Failure Patterns
Map the migration context against known Chaos Report risks:
- executive support
- user involvement
- scope clarity
- team size and skill mix
- delivery methodology

Explicitly call out mitigations for each.

### Step 2 — Assemble the Intelligent Team
Define a compact, cross-functional team:
- executive sponsor
- delivery lead / PM
- cloud architect
- developers
- DevOps
- QA

For each role:
- responsibility
- decision authority
- AI augmentation (GitHub Copilot, M365 Copilot)
- expected productivity impact

### Step 3 — Define the Phased Roadmap
Structure delivery into clear phases:
- initiation & planning
- environment & pipeline setup
- migration & development
- testing & hardening
- deployment & closure

Each phase must include:
- deliverables
- dependencies
- success criteria
- go/no-go decision points

### Step 4 — Embed Governance by Design
Define:
- review cadences
- escalation paths
- rollback strategies
- audit and telemetry expectations

Ensure Copilot is positioned as:
- drafting engine
- accelerator
- not decision authority

### Step 5 — Model Budget and ROI
Create:
- one-time migration cost model
- ongoing operational cost model
- productivity uplift assumptions
- ROI breakeven narrative

Tie assumptions explicitly to:
- team size
- AI augmentation level
- platform choice

## Output Quality Bar
- Programme increases success probability relative to industry baseline
- Risks are managed explicitly, not implicitly
- Outputs are suitable for executive review and funding approval
