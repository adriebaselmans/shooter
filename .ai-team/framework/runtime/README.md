# Runtime Support Utilities

This folder contains support utilities around the active flow-driven orchestrator.

## Purpose
- Persist the latest orchestration state snapshot in `state.json`.
- Keep release-doc export and repository support utilities available.
- Preserve machine-readable team metadata in `team.yaml`.

## Files
- `team.yaml`: role registry, ownership, and dispatch defaults.
- `state.json`: machine-readable runtime state snapshot written by the active orchestrator.
- `.ai-team/framework/memory/repository-knowledge/`: durable repository briefs and machine-readable facts for support exploration work.
- `spec_loader.py`: YAML spec loading.
- `artifacts.py` / `export_docs.py`: release-doc export helpers.
- `memory_store.py` / `memory_export.py`: optional structured memory helpers for bootstrapped project repos.
- `repository_tool.py`: repository exploration request builder.

## Runtime Rule
- The coordinator is the only top-level user-facing agent.
- The active orchestration logic lives in `.ai-team/team_orchestrator/`, `.ai-team/agents/`, `.ai-team/flows/`, and `.ai-team/state/`.
- The canonical CLI is `python -m team_orchestrator.cli` or `ai-dev-team-run`.
- Repository exploration is a shared support capability for grounding work.

## Prerequisites
- Python 3.12+
- `pip`

## Quickstart
Install dependencies:

```powershell
python -m pip install -e .
```

Run the active orchestrator:

```powershell
python -m team_orchestrator.cli run --input "Describe the feature here"
```

Check runtime state:

```powershell
python -m team_orchestrator.cli status
```

Repository exploration support is invoked by the coordinator when a task needs grounded repository analysis. It is not exposed as a standalone active role command in this runtime contract.

Generate release-only user docs from `doc_templates/`:

```powershell
python -m team_orchestrator.cli export-docs
```

Or use the release wrapper:

```powershell
pwsh -File .ai-team/framework/scripts/export-release-docs.ps1
```

Run that only on a release branch. The generated `docs/` output is intended for release check-in, not as an active source of truth.

Run runtime tests:

```powershell
python -m pytest .ai-team/framework/runtime/tests -q
```
