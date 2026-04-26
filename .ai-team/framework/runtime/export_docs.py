from __future__ import annotations

import subprocess
from pathlib import Path
from typing import Any

try:
    from .artifacts import ARTIFACT_FILES, load_artifact
    from .spec_loader import load_artifact_schema, repo_root
except ImportError:  # pragma: no cover - compatibility for direct script-style imports
    from artifacts import ARTIFACT_FILES, load_artifact
    from spec_loader import load_artifact_schema, repo_root

from team_orchestrator.project_context import release_docs_enabled


DOC_FILES = {
    "requirements": "docs/requirements/index.md",
    "design": "docs/design/index.md",
    "review": "docs/review/index.md",
    "dod": "docs/dod/index.md",
}


def doc_path(name: str) -> Path:
    try:
        return repo_root() / DOC_FILES[name]
    except KeyError as exc:
        raise KeyError(f"Unknown artifact export: {name}") from exc


def current_git_branch() -> str:
    result = subprocess.run(
        ["git", "branch", "--show-current"],
        cwd=repo_root(),
        check=True,
        capture_output=True,
        text=True,
    )
    return result.stdout.strip()


def ensure_release_branch() -> str:
    branch = current_git_branch()
    if not branch.startswith("release/"):
        display = branch or "detached HEAD"
        raise RuntimeError(f"export-docs is release-only. Current branch '{display}' is not a release branch.")
    return branch


def _render_value(value: Any, level: int = 0) -> list[str]:
    indent = "  " * level
    if isinstance(value, list):
        if not value:
            return [f"{indent}- None"]
        lines: list[str] = []
        for item in value:
            if isinstance(item, (dict, list)):
                lines.append(f"{indent}-")
                lines.extend(_render_value(item, level + 1))
            else:
                lines.append(f"{indent}- {item}")
        return lines
    if isinstance(value, dict):
        if not value:
            return [f"{indent}- None"]
        lines: list[str] = []
        for key, item in value.items():
            label = key.replace("_", " ").title()
            if isinstance(item, (dict, list)):
                lines.append(f"{indent}- {label}:")
                lines.extend(_render_value(item, level + 1))
            else:
                lines.append(f"{indent}- {label}: {item}")
        return lines
    if value in ("", None):
        return [f"{indent}- None"]
    return [f"{indent}- {value}"]


def _markdown_sections(schema: dict[str, Any], artifact_name: str) -> list[dict[str, str]]:
    sections = schema.get("markdown_sections")
    if not isinstance(sections, list) or not sections:
        raise ValueError(f"Schema '{artifact_name}' is missing markdown_sections.")
    normalized: list[dict[str, str]] = []
    for section in sections:
        if not isinstance(section, dict):
            raise ValueError(f"Schema '{artifact_name}' has an invalid markdown section entry.")
        heading = section.get("heading")
        field = section.get("field")
        if not isinstance(heading, str) or not heading.strip():
            raise ValueError(f"Schema '{artifact_name}' has a markdown section with no heading.")
        if not isinstance(field, str) or not field.strip():
            raise ValueError(f"Schema '{artifact_name}' has a markdown section with no field.")
        normalized.append({"heading": heading.strip(), "field": field.strip()})
    return normalized


def render_artifact_doc(name: str) -> str:
    schema = load_artifact_schema(name)
    payload = load_artifact(name)
    title = schema.get("title", name.title())
    lines = [f"# {title}", "", f"Generated from `{ARTIFACT_FILES[name]}`.", ""]
    for section in _markdown_sections(schema, name):
        lines.append(f"## {section['heading']}")
        lines.extend(_render_value(payload.get(section["field"])))
        lines.append("")
    return "\n".join(lines).rstrip() + "\n"


def export_all_docs(*, release_only: bool = False) -> list[Path]:
    if not release_docs_enabled(repo_root()):
        raise RuntimeError("export-docs is only enabled for bootstrapped project repos created from this skeleton.")
    if release_only:
        ensure_release_branch()
    written: list[Path] = []
    for name in ARTIFACT_FILES:
        path = doc_path(name)
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_text(render_artifact_doc(name), encoding="utf-8")
        written.append(path)
    return written
