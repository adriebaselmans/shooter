# Native Agents

These custom agent profiles are the primary execution surface for this skeleton in GitHub Copilot in Visual Studio Code.

The canonical framework contract still lives in:
- `.ai-team/framework/AGENTS.md`
- `.ai-team/framework/runtime/team.yaml`
- `.ai-team/flows/software_delivery.yaml`
- `.ai-team/framework/roles/*.md`
- `.ai-team/framework/prompts/*`

Each agent profile mirrors one framework role. The coordinator owns user-facing intake and handoffs. Support roles are only used through coordinator-mediated handoffs.
All workspace agent profiles are explicitly targeted at VS Code. Only the coordinator is intended to be user-selectable; specialist roles stay hidden from the picker and are reached only through coordinator-mediated subagent dispatch.
Per-role Copilot model preferences are sourced from `.ai-team/framework/config/copilot_role_models.yaml`. The profiles mirror that file through the `model` frontmatter field.
Treat `.ai-team/framework/config/copilot_role_models.yaml` as the source of truth. If it changes, sync the `.github/agents/*.agent.md` `model` frontmatter and rerun the native-agent profile tests.

Use stable Copilot tool aliases in agent frontmatter. Prefer role-based aliases such as `read`, `edit`, `search`, `execute`, `agent`, and `web` over brittle host-internal tool names. Give specialists the capabilities they actually need so they do not waste tokens working around missing built-in tools.
