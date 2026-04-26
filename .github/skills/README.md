# Project Skills

This repository uses project-local skills as the operational layer beneath the team roles.

## Purpose
- Roles define authority, responsibility, boundaries, and required outputs.
- Skills define repeatable execution patterns for recurring work.

## Contract Convention
- Each skill folder should include a `contract.yaml` file that declares required inputs, expected outputs, owned artifacts, and completion criteria.
- Skills are composable units, so higher-level roles can combine them when the work warrants it.
- Repository exploration is a shared support skill, not a standalone team phase.

## Primary Project Skills
- `coordinator-flow`
- `repository-exploration`
- `compaction`
- `repository-knowledge-compaction`
- `requirements-clarification`
- `architecture-design`
- `implementation-clean-code`
- `code-review`
- `unit-testing`
- `acceptance-testing`
- `memory-update`

## Reusable OpenAI Skills
These are useful when installed and available in Codex:
- `openai-docs`: use for official OpenAI or model/platform guidance.
- `playwright`: use for browser-based acceptance or end-to-end testing when the stack supports it.
- `security-best-practices`: use when a feature has meaningful security impact.
- `security-threat-model`: use when a feature changes trust boundaries, inputs, or access patterns.
- `doc`: use for documentation-heavy tasks when a general documentation helper is useful.

## Usage Rule
- Use the role file to decide who owns the work.
- Use the matching skill to decide how to perform the work.
- Prefer project-local skills for team-specific flow and artifacts.
- Prefer reusable external skills for general-purpose capability when they fit cleanly.
- The `repository-exploration` skill is intended to be directly user-invokable when a user asks for grounded analysis of a specific repository.
