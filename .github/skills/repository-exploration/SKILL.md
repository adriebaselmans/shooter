---
name: repository-exploration
description: Analyze an existing code repository quickly, extract the architecture and conventions that matter, and create a compact reusable repository brief for the AI team or the user.
---

# Repository Exploration

Use this skill when the user asks to base work on a specific repository, asks for a repo analysis directly, or when another team role is blocked on missing repository context.

## Goals
- Build grounded understanding of the target repository fast.
- Produce compact reusable knowledge instead of repeating the same discovery work.
- Surface what is known, what is inferred, and what remains uncertain.

## Required Inputs
- Target repository path, URL, or checkout location
- The user request or downstream role question that motivates the exploration
- Existing repository knowledge in `.ai-team/framework/memory/wiki/repositories/` when available

## Required Outputs
- `.ai-team/framework/memory/wiki/repositories/<repo-slug>/brief.md`
- `.ai-team/framework/memory/wiki/repositories/<repo-slug>/facts.yaml`
- Updated `wiki/repositories/_index.yaml` when the repository is new to the knowledge store

## Procedure
1. Identify the target repository, its local path or source URL, and its current revision when available.
2. Detect the stack from manifests, lockfiles, build files, and top-level docs.
3. Find the important entry points, packages, services, and runtime boundaries.
4. Trace the user-relevant or task-relevant flows instead of reading the entire repo indiscriminately.
5. Record key modules, architectural patterns, naming conventions, testing approach, and build or run commands.
6. Separate direct evidence from informed inference.
7. Hand off compact findings that another role can use without rescanning the same repo.

## Exploration Rules
- Prefer breadth-first orientation first, then drill into task-relevant areas.
- Use file references and revision identifiers so later agents can verify claims quickly.
- Keep the brief short enough to fit comfortably into future context windows.
- Capture open questions explicitly when the evidence is incomplete.
- Do not rewrite the whole repository into memory.

## Brief Shape
The brief should cover:
- Repository identity and revision
- Purpose and top-level architecture
- Stack and tooling
- Key directories and entry points
- Important flows or subsystems for the current task
- Conventions, risks, and extension points
- Open questions
