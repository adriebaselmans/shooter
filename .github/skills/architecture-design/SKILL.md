---
name: architecture-design
description: Produce a compact technical design that satisfies the active requirements baseline, enforces clean-code principles, uses current best practices for the chosen stack, and makes important performance tradeoffs explicit.
---

# Architecture Design

Use this skill when acting as the architect in this repository.

## Goals
- Create an implementation-safe design.
- Keep the design simple, clean, and testable.
- Make relevant CPU and memory tradeoffs explicit.

## Required Inputs
- `doc_templates/requirements/current.yaml`
- `doc_templates/design/current.yaml`
- `.ai-team/framework/clean-code.md`
- Relevant code in `src/`
- Project wiki via `wiki-read` skill

## Required Output
- Updated `doc_templates/design/current.yaml`

## Procedure
1. Read the active requirements and current codebase.
2. Identify affected modules, boundaries, and contracts.
3. Research the best current stack-appropriate approach when a meaningful technical choice exists.
4. Choose the simplest design that satisfies the requirements and supports maintainable implementation.
5. Document separation of concerns, module ownership, interfaces, data flow, clean-code constraints, performance considerations, and non-goals.

## Design Rules
- Prefer current stable ecosystem practices over outdated patterns.
- Avoid speculative architecture.
- Separate business logic, orchestration, and I/O where practical.
- Make non-trivial CPU or memory tradeoffs explicit.
- Return to requirements if functional ambiguity blocks safe design.
