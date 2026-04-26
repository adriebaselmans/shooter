---
name: requirements-clarification
description: Turn a user need into an implementation-ready requirements baseline with minimal but sufficient clarification, explicit scope boundaries, and testable acceptance criteria.
---

# Requirements Clarification

Use this skill when acting as the requirements engineer in this repository.

## Goals
- Remove blocking ambiguity.
- Produce a compact, testable requirement baseline.
- Allow the team to proceed autonomously once the phase is complete.

## Required Inputs
- User need from the coordinator
- `doc_templates/requirements/current.yaml`
- Relevant project memory in `.ai-team/framework/memory/`

## Required Output
- Updated `doc_templates/requirements/current.yaml`
- Clarification questions for the coordinator when needed

## Procedure
1. Restate the user need clearly.
2. Separate in-scope from out-of-scope work.
3. Write concrete functional requirements.
4. Convert the outcome into testable acceptance criteria.
5. Record constraints, assumptions, and any remaining open questions.
6. Mark the feature ready only when architecture and implementation can proceed without guessing.

## Clarification Rule
- Ask only the minimum questions needed to remove blocking ambiguity.
- Draft those questions for the coordinator to relay.

## Quality Bar
- Requirements must be specific, testable, and implementation-relevant.
- Avoid embedding technical design choices unless they are true constraints.
