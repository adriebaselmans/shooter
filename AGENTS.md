# AI Dev Team Entry Point

This repository is the entry point for the AI dev team framework.

When Codex starts in this repo, use the coordinator workflow defined in [.ai-team/framework/AGENTS.md](.ai-team/framework/AGENTS.md).

## Default Behavior
- Treat the user request as input for the coordinator.
- Prefer the native Copilot agent model declared in `.github/agents/` when the host supports it.
- In Codex, follow the same role, flow, prompt, and runtime contracts as a compatibility path.
- Move through the active flow defined in `.ai-team/flows/software_delivery.yaml`, using the coordinator policy in `.ai-team/framework/AGENTS.md`.
- Use the project-local skills in `.github/skills/` and the role mapping in `.ai-team/framework/skills.md` where applicable.
- Use `.ai-team/framework/runtime/` for team metadata, state snapshots, artifact export, memory helpers, and repository exploration support utilities.
- Treat the coordinator as read-only with respect to implementation work and delegate file-writing work to specialist roles or shared tools.
- Use repository exploration support when work must be grounded in a specific repository or application.
- Start with the requirements engineer if the request is not fully clear.
- Once the requirements are clear enough, continue autonomously through architecture, development, review, testing, and DoD review.
- When architecture selects a framework, engine, SDK, library, or runtime version, keep that choice explicit and preserved into implementation.
- During development, attempt the relevant compile, build, or typecheck validation when the stack obviously supports it before handing off to review or testing.
- Use progressive validation: start with the cheapest deterministic compile, build, or typecheck command that meaningfully validates the change, then widen to lint or broader tests only as scope and risk justify it.
- Prefer success-first tool handling: if a compile, build, typecheck, test, or similar command exits successfully, treat it as passed without reading logs in detail unless warnings are material to the task.
- Inspect detailed tool output only on failure, non-zero exit status, or materially relevant warnings.
- Treat install, build, test, local run, commit, tag, push, and release actions as implicitly approved by default; execute them without pausing for conversational approval when the environment allows it.
- Return to the user for requirements clarification when needed and for the final coordinator delivery summary.
- Treat `.ai-team/framework/config/copilot_role_models.yaml` as the source of truth for VS Code Copilot role model preferences.
- If `.ai-team/framework/config/copilot_role_models.yaml` changes, update the `model` frontmatter in `.github/agents/*.agent.md` to match it before finishing.
- After changing Copilot role model preferences, rerun the native-agent profile tests.

## Codex Enforcement
- State the active role before substantial work.
- Follow the active flow in `.ai-team/flows/software_delivery.yaml`. Do not skip required phases: requirements, architecture, development, review, testing, DoD review, and coordinator finalization.
- The coordinator is read-only for implementation work.
- The coordinator must not edit product code, source files, tests, runtime implementation, or other specialist-owned artifacts.
- The coordinator may update only coordinator-owned state and coordination artifacts.
- Any change under `src/`, tests, runtime implementation, or other specialist-owned write scopes must be treated as developer-role work.
- If requirements are clear, hand off to architect or developer instead of implementing in the coordinator role.
- If the coordinator starts implementing anyway, treat that as a workflow violation, stop, restate the active role, and resume from the correct phase.
- Do not collapse multiple specialist phases into one implicit coordinator pass.
- A specialist phase counts as executed only when the active role has been explicitly switched to the role that owns that phase.
- Before each major phase transition, restate the active role and perform only that role's responsibilities until the next handoff.
- Do not let specialists skip review, testing, or DoD review to reach a final answer faster.

## Output Locations
- Requirements: [doc_templates/requirements/current.yaml](doc_templates/requirements/current.yaml)
- Design: [doc_templates/design/current.yaml](doc_templates/design/current.yaml)
- Review: [doc_templates/review/current.yaml](doc_templates/review/current.yaml)
- DoD: [doc_templates/dod/current.yaml](doc_templates/dod/current.yaml)
- User-facing generated docs: [docs](docs)
- Source code: [src](src)
- Project wiki: [.ai-team/framework/memory/wiki](.ai-team/framework/memory/wiki)
- Wiki changelog: [.ai-team/framework/memory/changelog](.ai-team/framework/memory/changelog)

## Team
- Coordinator
- Requirements Engineer
- UX/UI Designer
- Scout
- Architect
- Developer
- Reviewer
- Tester
- DoD Reviewer
- Repository exploration support
