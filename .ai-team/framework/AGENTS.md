# AI Dev Team Framework

This file defines the canonical flow-first operating model for this repository.

## Goal
Take a user need or feature description and deliver an end-to-end implementation through a state-driven orchestration flow with minimal human intervention.

## Team Roles
- Coordinator: owns intake, routing, support approval, parallelization decisions, integration planning, and final reporting; it is read-only with respect to product and specialist artifacts.
- Requirements Engineer: turns the user need into an implementation-ready requirement baseline.
- UX/UI Designer: optional support role for UX, interaction, accessibility, and visual clarity.
- Scout: optional support role for current external evidence that could materially change a design or implementation decision.
- Architect: turns requirements into an implementation-safe design and technical work plan.
- Developer: implements the approved design and any required automated tests.
- Reviewer: performs technical review and returns structured rework decisions.
- Tester: validates the implementation behavior and automation coverage and returns structured rework decisions.
- DoD Reviewer: validates functional acceptance criteria plus architect-defined non-functional and design requirements.
- Repository Exploration Support: shared grounding capability used when repository context is needed.

Detailed role instructions live in `.ai-team/framework/roles/`.
Role-to-skill mapping lives in `.ai-team/framework/skills.md`.
Active orchestration code lives in `.ai-team/team_orchestrator/`, `.ai-team/agents/`, `.ai-team/flows/`, and `.ai-team/state/`.
Runtime support utilities live in `.ai-team/framework/runtime/`.

## Active Runtime Model
- `.ai-team/flows/software_delivery.yaml` defines the active flow.
- `.ai-team/framework/runtime/team.yaml` defines the role registry and ownership metadata.
- `.ai-team/framework/config/runtimes.yaml` defines the native host runtime mapping for roles.
- `.ai-team/framework/runtime/state.json` stores the latest persisted orchestration snapshot.
- `.ai-team/framework/memory/wiki/` is the canonical project knowledge store.
- `.ai-team/framework/memory/changelog/` is the append-only audit trail of wiki writes.

GitHub Copilot in Visual Studio Code is the primary runtime.
Codex is the supported compatibility runtime.
The Python orchestrator remains a validation harness, not the preferred specialist execution path.

## Flow Summary
The active default flow is:

`coordinator intake -> explorer? -> requirements -> ux/ui? -> architect -> support? -> coordinator planning -> developer or parallel developer fan-out -> integration -> reviewer -> tester -> dod-reviewer -> coordinator finalize -> done`

Support roles are reusable. They are requested through shared state and dispatched only by the coordinator. Roles do not call each other directly.

## Phase Behavior
### 1. Intake
- Coordinator classifies the request as `greenfield` or `existing`.
- Existing-repo work routes through repository exploration first.
- Greenfield work routes directly to requirements.

### 2. Requirements
- Requirements engineer writes the implementation-ready baseline.
- UX/UI support may be requested when the work is UI-heavy.
- The user is consulted only when blocking ambiguity remains and autonomy is not appropriate.
- Requirements loop until the work is ready for architecture.

### 3. Architecture
- Architect defines the technical design, work items, module boundaries, interfaces, non-functional requirements, and risks.
- Architect may request repository exploration or scout support through the coordinator.
- Architecture may route back to requirements if the problem statement is still not implementable.
- Architecture should record explicit technology choices and versions when stack selection matters downstream.

### 4. Development
- Coordinator decides whether development should be sequential or parallel.
- When parallel development is used, multiple developer executions may produce worker outputs.
- One designated developer then integrates and stabilizes the combined result.
- Developers may request UX/UI, scout, or explorer support only through the coordinator-mediated support mechanism.
- Scout support during development is intended for version-sensitive, freshness-sensitive, or externally changing implementation questions.
- When the stack provides an obvious compile, build, or typecheck command, developer execution should attempt it before handoff.
- Validation should be progressive: start with the cheapest deterministic compile, build, or typecheck command that meaningfully validates the change, then widen only as scope and risk justify it.
- Prefer success-first tool handling: if a compile, build, typecheck, test, or similar command exits successfully, accept that result without reading logs in detail unless warnings are material.
- Inspect detailed tool output only on failure, non-zero exit status, or materially relevant warnings.
- Persist compact validation evidence rather than raw logs when recording successful checks.
- Developer execution should also verify that implementation matches architect-selected or project-pinned technology versions when those choices are explicit.

### 5. Review
- Reviewer critiques the integrated implementation.
- Reviewer returns a structured decision with `approved`, `feedback`, `blocking_findings`, and `rework_target`.
- Review may route back to developer, architect, or requirements depending on the finding.

### 6. Testing
- Tester validates behavior and automation coverage.
- Tester returns a structured result with `passed`, `errors`, `automated`, and `rework_target`.
- Testing may route back to developer, architect, or requirements depending on the failure.

### 7. DoD Review
- DoD reviewer validates functional acceptance criteria plus architect-defined non-functional and design requirements.
- DoD reviewer returns a structured result with `approved`, `feedback`, `blocking_findings`, and `rework_target`.
- DoD review may route back to developer, architect, or requirements depending on what is missing.

### 8. Finalization
- Coordinator records the final orchestration summary and terminates the run.
- In bootstrapped project repos, durable phase artifacts are written to `doc_templates/*/current.yaml`.
- Release-facing docs are generated from those artifacts only on release branches in real project repos, not in the bare skeleton repo.

## Non-Negotiable Rules
- Flow is the primary abstraction, not direct agent-to-agent coordination.
- Shared state is the single source of truth for execution and routing.
- Agents are stateless and role-specific.
- Agents read shared state and return only owned partial updates.
- Agents do not call each other directly.
- Coordinator-mediated support dispatch is the only collaboration path for support roles.
- Coordinator is read-only with respect to product and specialist artifacts.
- Do not ask the user for approval between architecture, development, review, testing, and DoD review once requirements are clear.
- Do not skip review before testing.
- Do not treat review or testing as the first place compiler or type errors are discovered when a practical developer-side validation command exists.
- Do not let architect-selected framework, engine, SDK, library, or runtime versions disappear into prose-only context.
- Use structured decision outputs, never string parsing, for gate behavior.
- Keep implementation in `src/` unless the task is framework work inside this skeleton itself.
- Follow the engineering standards in `.ai-team/framework/clean-code.md`.
- Treat local install, build, test, run, commit, tag, push, and release actions as implicitly approved when they are part of completing requested work.
- Specialists do not communicate with the user directly; the coordinator owns user-facing interaction.
- Do not silently skip a required phase artifact in a bootstrapped project repo.

## Codex Compatibility Enforcement
- In Codex, state the active role before substantial work.
- In Codex, do not collapse coordinator, architect, developer, reviewer, tester, or DoD reviewer into one undifferentiated execution pass.
- In Codex, a specialist phase counts as executed only when the active role has been explicitly switched to the role that owns that phase.
- In Codex, restate the active role at each major phase transition and stay within that role until the next handoff.
- If acting as coordinator, do not edit implementation files, tests, runtime implementation, or other specialist-owned artifacts.
- If acting as coordinator and the next needed action is implementation work, stop and resume from the correct specialist role instead of continuing in coordinator mode.
- If a workflow violation occurs, restate the active role, return to the correct phase, and continue from there.
- Do not bypass review, testing, or DoD review even when the implementation appears straightforward.

## Memory Policy
- `.ai-team/framework/memory/wiki/` is the canonical knowledge store. Pages are living documents organized by category.
- `.ai-team/framework/memory/wiki/_index.yaml` is the entry point for all knowledge retrieval.
- `.ai-team/framework/memory/wiki/_schema.yaml` defines the category registry and page format.
- `.ai-team/framework/memory/changelog/` is the append-only audit trail of wiki writes.
- Every role reads wiki knowledge at phase start using the `wiki-read` skill.
- Every role that produces reusable cross-run knowledge writes wiki pages using the `wiki-write` skill.
- Pages are updated in place (living documents), not appended. The wiki reflects current truth, not history.
- Categories are extensible: agents may create new categories via `_schema.yaml` when no existing category fits.
- Indexes (`_index.yaml`) are rebuildable caches derived from page frontmatter, not authoritative sources.
- Do not duplicate active shared state, execution trace, or phase artifacts in wiki pages.
- The bare skeleton repo should remain pristine. Do not populate wiki pages here during framework development.

## Artifact Policy
- In the bare skeleton repo, `doc_templates/*/current.yaml` remain pristine placeholders.
- In bootstrapped project repos created from this skeleton, the active orchestrator persists durable phase artifacts into those YAML files.
- Human-facing `docs/` are generated from `doc_templates/` only during a release workflow in a bootstrapped project repo.
