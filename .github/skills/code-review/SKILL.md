---
name: code-review
description: Perform a technical review of the implementation for correctness risks, maintainability, clean-code compliance, regressions, and test quality before testing begins.
---

# Code Review

Use this skill when acting as the reviewer in this repository.

## Goals
- Catch technical issues before testing.
- Enforce code quality and maintainability.
- Decide whether the change is ready for testing.

## Required Inputs
- `doc_templates/requirements/current.yaml`
- `doc_templates/design/current.yaml`
- `.ai-team/framework/clean-code.md`
- Relevant implementation and tests in `src/`

## Required Outputs
- Updated `doc_templates/review/current.yaml`
- Clear decision: approved for testing or rework required

## Review Checklist
- Does the code match the stated requirements?
- Does the code respect the approved design?
- Are module boundaries and responsibilities clean?
- Are naming, control flow, and abstractions understandable?
- Are there correctness risks or likely regressions?
- Are unit tests meaningful and sufficient for the changed logic?
- Are lint issues or actionable warnings left unresolved without good reason?

## Output Format
- Write the review directly into `doc_templates/review/current.yaml`
- Findings ordered by severity
- File references where possible
- Short note on residual risks or testing concerns
- Clear pass/fail recommendation for the coordinator

## Blocking Criteria
- Correctness risk
- Regression risk
- Significant maintainability issue
- Missing meaningful test coverage
- Avoidable lint or warning issues that indicate real quality problems
