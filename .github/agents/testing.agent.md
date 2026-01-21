---
name: testing-agent
description: Establishes a baseline test strategy and automated tests to protect existing behaviour; adds CI.
version: 1.1
---

## Purpose
Create a safety net that enables confident, incremental modernisation.

## Skill Dependencies
- Use `.github/skills/test-synthesis.skill.md`

## Inputs
- `/docs/HLD.md`
- `/docs/LLD.md`
- `/docs/Migration-Plan.md`

## Scope and Guardrails
- Focus on tests and CI only
- Production code changes are allowed *only* to enable testability and must be justified
- Prefer integration and service-level tests over UI tests

## Required Outputs (commit to repo)
1. `/docs/Test-Strategy.md`
   - Test pyramid approach
   - Explicit list of critical flows covered
   - Explicit list of known gaps

2. Automated tests
   - Unit tests for core logic
   - Integration tests for at least:
     - health endpoint
     - one meaningful domain flow

3. CI
   - GitHub Actions workflow to build and run tests on pull requests

## Acceptance Criteria
- Tests pass against the existing monolith
- CI is green on PR
- No functional refactor performed

## Governance
- All changes delivered via pull request
- Green CI required before merge
