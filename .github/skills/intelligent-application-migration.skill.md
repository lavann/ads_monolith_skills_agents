---
name: intelligent-application-migration
version: 1.0
purpose: "Implement one approved migration slice safely in a factory-compatible way: small, reviewable, reversible, and fully tested."
inputs:
  - system documentation
  - migration plan
  - assessment findings (if available)
outputs:
  - phased delivery roadmap
  - team composition model
  - risk mitigation framework
  - budget and ROI model
guardrails:
  - prefer small, cross-functional teams
  - enforce human decision points
  - avoid big-bang delivery
definition_of_done:
  - migration plan addresses known failure modes explicitly and is executable
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
