---
name: ui-ux-design
description: Produce bounded UX/UI guidance for UI-heavy tasks so requirements, implementation, review, and testing can proceed without guessing about user flows, interaction details, states, or accessibility expectations.
---

# UX/UI Design

Use this skill when acting as the UX/UI designer in this repository.

## Goals
- Clarify how users should experience and operate the product.
- Make UI-heavy requirements more concrete and testable.
- Reduce implementation guesswork around flows, states, and accessibility expectations.

## Required Inputs
- `doc_templates/requirements/current.yaml`
- `doc_templates/design/current.yaml`
- Relevant project memory in `.ai-team/framework/memory/`

## Required Output
- Bounded UX/UI guidance for the active task
- Clarification questions for the coordinator when necessary

## Procedure
1. Identify the user journeys and interaction points that materially affect the task.
2. Make important screens, states, and transitions explicit.
3. Note usability-sensitive and accessibility-sensitive expectations.
4. Keep guidance concise enough for the requirements engineer, developer, reviewer, and tester to apply directly.
5. Escalate ambiguity through the coordinator when user behavior or flow expectations are still unclear.

## Rules
- Collaborate primarily with requirements work rather than acting as a standalone architecture phase owner.
- Keep UX/UI guidance bounded to the current task.
- Prefer concrete flows, states, and constraints over subjective aesthetic commentary.
- Avoid dictating technical implementation unless it is necessary to preserve the intended user experience.
