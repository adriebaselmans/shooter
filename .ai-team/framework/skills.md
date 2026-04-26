# Skills Mapping

This file maps team roles to project-local skills and reusable external skills.

## Source Policy
- Tier 1 external source: `openai/skills`
- Tier 2 external sources: `anthropics/skills`, `vercel-labs/agent-skills`, `MicrosoftDocs/Agent-Skills`
- Prefer project-local skills for workflow, artifacts, memory, and repository-specific conventions.
- Use external skills for specialized domain guidance or execution help when they clearly improve success without fighting the repository's role model.
- Do not install or use community-curated skills by default; treat them as discovery-only unless manually vetted.

## Role To Skill Mapping
- Coordinator
  Primary: `.github/skills/coordinator-flow`
  Supporting: `.github/skills/memory-update`
  Runtime: `.ai-team/flows/software_delivery.yaml`, `.ai-team/framework/runtime/team.yaml`, `.ai-team/framework/config/runtimes.yaml`
- Requirements Engineer
  Primary: `.github/skills/requirements-clarification`
  Optional external: `pdf`
- UX/UI Designer
  Primary: `.github/skills/ui-ux-design`
  Optional external: `playwright`, `webapp-testing`, `web-design-guidelines`, `composition-patterns`
- Scout
  Primary: `.github/skills/external-research`
- Architect
  Primary: `.github/skills/architecture-design`
  Optional external: `openai-docs`, `security-threat-model`, `security-best-practices`, `azure-well-architected`
- Developer
  Primary: `.github/skills/implementation-clean-code`
  Supporting: `.github/skills/unit-testing`
  Optional external: `openai-docs`, `gh-fix-ci`, `security-best-practices`, `playwright`, `react-best-practices`, `composition-patterns`
- Reviewer
  Primary: `.github/skills/code-review`
  Optional external: `security-best-practices`, `gh-fix-ci`, `web-design-guidelines`
- Tester
  Primary: `.github/skills/acceptance-testing`
  Optional external: `playwright`, `security-best-practices`, `webapp-testing`, `web-design-guidelines`
- DoD Reviewer
  Primary: `.github/skills/acceptance-testing`
  Optional external: `playwright`, `security-best-practices`, `web-design-guidelines`

## Shared Support Skills
- Repository exploration: `.github/skills/repository-exploration`
- Repository knowledge compaction: `.github/skills/repository-knowledge-compaction`

## Shared Wiki Skills (all roles)
- Wiki read: `.github/skills/wiki-read`
- Wiki write: `.github/skills/wiki-write`

## Installed External Skills
Tier 1:
- `openai-docs`
- `playwright`
- `security-best-practices`
- `security-threat-model`
- `gh-fix-ci`

Tier 2:
- `webapp-testing`
- `react-best-practices`
- `composition-patterns`
- `web-design-guidelines`
- `azure-well-architected`

## Trigger Guidance
- Use `openai-docs` when architecture or implementation work depends on current OpenAI platform guidance.
- Use `scout` when architecture or implementation depends on current external information that is temporally unstable, version-sensitive, freshness-sensitive, or meaningfully uncertain.
- Use `playwright` or `webapp-testing` when tester or developer work requires real browser automation.
- Use `security-best-practices` for explicit security review or secure-by-default implementation choices.
- Use `security-threat-model` for explicit security architecture and abuse-path analysis.
- Use `gh-fix-ci` when developer or reviewer work is blocked by failing GitHub Actions checks.
- Use `react-best-practices`, `composition-patterns`, and `web-design-guidelines` only for frontend-heavy work.
- Use `web-design-guidelines` and `composition-patterns` for UX/UI design work that needs stronger interaction and component guidance.
- Use `playwright` or `webapp-testing` for UI verification when the UX/UI designer is validating real flows or states.
- Use `azure-well-architected` only when Azure architecture is in scope.

## Rule
- For active runtime orchestration, treat `.ai-team/flows/software_delivery.yaml`, `.ai-team/framework/runtime/team.yaml`, and `.ai-team/framework/config/runtimes.yaml` as the machine-readable contract set.
