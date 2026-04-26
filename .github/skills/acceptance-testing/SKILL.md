---
name: acceptance-testing
description: Create or maintain automated acceptance or end-to-end tests when feasible, validate important user flows, and document when such automation is not practical.
---

# Acceptance Testing

Use this skill when acting as the tester for a feature where acceptance or end-to-end automation is feasible and worthwhile.

## Goals
- Validate user-visible behavior at acceptance level.
- Strengthen regression protection for important flows.
- Leave behind a rerunnable automated regression suite whenever the feature can support it.

## Required Inputs
- `doc_templates/requirements/current.yaml`
- `doc_templates/design/current.yaml`
- Implementation and existing tests
- Current DoD artifact

## Required Outputs
- Automated acceptance or end-to-end regression tests when feasible
- Clear rerun command or instructions for that regression suite
- Clear note when such automation is not feasible or not cost-effective

## Rules
- Cover the most important user flows first.
- Prefer stable, maintainable test flows over brittle end-to-end scripts.
- Map automated checks back to the acceptance criteria wherever practical.
- Treat manual-only validation as a fallback, not the default, when a stable automated path exists.
- Be explicit about what the tests prove and what they do not prove.
