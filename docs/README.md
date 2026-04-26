# User Docs

This tree is reserved for user-facing generated documentation.

- `doc_templates/` contains the AI-owned source-of-truth YAML artifacts.
- `docs/` contains generated human-facing output only.
- Generate this content explicitly, typically as part of a release workflow.
- Use `python -m team_orchestrator.cli export-docs` or `pwsh -File .ai-team/framework/scripts/export-release-docs.ps1` on a `release/*` branch.
- Do not treat files under `docs/` as active phase artifacts.
