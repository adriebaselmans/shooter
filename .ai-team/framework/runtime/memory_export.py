from __future__ import annotations

from pathlib import Path
from typing import Any

import yaml

try:
    from .memory_store import query_memory, query_wiki, wiki_root
except ImportError:  # pragma: no cover - compatibility for direct script-style imports
    from memory_store import query_memory, query_wiki, wiki_root


def render_project_log_snapshot(*, limit: int = 20, root: Path | None = None) -> str:
    """Render a project log from wiki project pages."""
    pages = query_wiki(category="project", limit=limit, root=root)
    if not pages:
        return "# Project Log Snapshot\n\nNo wiki pages in project category.\n"
    lines = ["# Project Log Snapshot", ""]
    for page in pages:
        ts = page.get("updated", "unknown-time")
        summary = page.get("summary", "No summary")
        lines.append(f"- {ts}: {summary}")
        lines.append(f"  Category: {page.get('cat', 'unknown')}")
        lines.append(f"  Tags: {', '.join(page.get('tags', []))}")
    return "\n".join(lines).strip() + "\n"


def render_decisions_snapshot(*, limit: int = 20, root: Path | None = None) -> str:
    """Render decisions from wiki decisions pages."""
    pages = query_wiki(category="decisions", limit=limit, root=root)
    if not pages:
        return "# Decisions Snapshot\n\nNo wiki pages in decisions category.\n"
    lines = ["# Decisions Snapshot", ""]
    for page in pages:
        ts = page.get("updated", "unknown-time")
        summary = page.get("summary", "No summary")
        lines.append(f"- {ts}: {summary}")
        lines.append(f"  Author: {page.get('by', 'unknown')}")
        lines.append(f"  Rev: {page.get('rev', 1)}")
    return "\n".join(lines).strip() + "\n"


def render_known_context_snapshot(*, fact_limit: int = 20, decision_limit: int = 10, root: Path | None = None) -> str:
    """Render known context from wiki context and decisions pages."""
    context_pages = query_wiki(category="context", limit=fact_limit, root=root)
    decision_pages = query_wiki(category="decisions", limit=decision_limit, root=root)
    lines = ["# Known Context Snapshot", ""]

    lines.append("## Active Context")
    if not context_pages:
        lines.append("- None")
    else:
        for page in context_pages:
            ts = page.get("updated", "unknown-time")
            lines.append(f"- {ts}: {page.get('summary', 'No summary')}")

    lines.append("")
    lines.append("## Active Decisions")
    if not decision_pages:
        lines.append("- None")
    else:
        for page in decision_pages:
            ts = page.get("updated", "unknown-time")
            lines.append(f"- {ts}: {page.get('summary', 'No summary')}")

    return "\n".join(lines).strip() + "\n"


def render_memory_snapshot(view: str, *, limit: int = 20, root: Path | None = None) -> str:
    if view == "project-log":
        return render_project_log_snapshot(limit=limit, root=root)
    if view == "decisions":
        return render_decisions_snapshot(limit=limit, root=root)
    if view == "known-context":
        return render_known_context_snapshot(fact_limit=limit, decision_limit=max(1, limit // 2), root=root)
    raise ValueError(f"Unknown memory snapshot view '{view}'.")
