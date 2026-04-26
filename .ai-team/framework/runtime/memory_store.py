from __future__ import annotations

import json
import re
from dataclasses import asdict, dataclass
from datetime import datetime, timezone
from pathlib import Path
from typing import Any, Iterable
from uuid import uuid4

import yaml

try:
    from .spec_loader import repo_root
except ImportError:  # pragma: no cover - compatibility for direct script-style imports
    from spec_loader import repo_root


MEMORY_SCHEMA_VERSION = 4
DEFAULT_SCOPE = "project"
DEFAULT_SOURCE = "runtime"
DEFAULT_CONFIDENCE = "medium"
DEFAULT_STATUS = "active"
VALID_RECORD_KINDS = {"fact", "decision", "question", "contradiction", "phase-brief"}
VALID_RECORD_STATUSES = {"active", "superseded", "resolved"}
VALID_CONFIDENCE_LEVELS = {"low", "medium", "high"}

# Wiki page status values
VALID_PAGE_STATUSES = {"active", "stale", "archived"}

# Category-to-record-kind mapping for wiki placement
KIND_CATEGORY_MAP: dict[str, str] = {
    "fact": "context",
    "decision": "decisions",
    "question": "context",
    "contradiction": "incidents",
    "phase-brief": "project",
}


@dataclass(frozen=True)
class MemoryRecord:
    entry_id: str
    timestamp_utc: str
    version: int
    kind: str
    phase: str
    scope: str
    subject: str | None
    source: str
    confidence: str
    status: str
    tags: list[str]
    summary: str
    artifact_refs: list[str]
    payload: dict[str, Any]
    supersedes: str | None = None

    def as_dict(self) -> dict[str, Any]:
        payload = asdict(self)
        if self.supersedes is None:
            payload.pop("supersedes", None)
        if self.subject is None:
            payload.pop("subject", None)
        return payload


def memory_root(root: Path | None = None) -> Path:
    base = root or repo_root()
    return base / ".ai-team" / "framework" / "memory"


def wiki_root(root: Path | None = None) -> Path:
    return memory_root(root) / "wiki"


def changelog_root(root: Path | None = None) -> Path:
    return memory_root(root) / "changelog"


def records_root(*, create: bool = True, root: Path | None = None) -> Path:
    path = memory_root(root) / "records"
    if create:
        path.mkdir(parents=True, exist_ok=True)
    return path


def legacy_records_roots(root: Path | None = None) -> list[Path]:
    base = root or repo_root()
    return [
        base / "framework" / "memory" / "logs",
        memory_root(root) / "logs",
    ]


def timestamp_utc() -> str:
    return datetime.now(timezone.utc).strftime("%Y-%m-%dT%H:%M:%S.%fZ")


# ---------------------------------------------------------------------------
# Wiki page I/O
# ---------------------------------------------------------------------------

_FRONTMATTER_RE = re.compile(r"^---\s*\n(.*?)\n---\s*\n", re.DOTALL)


def _parse_wiki_page(path: Path) -> dict[str, Any] | None:
    """Parse a wiki page and return its frontmatter as a dict, or None."""
    if not path.exists():
        return None
    text = path.read_text(encoding="utf-8")
    match = _FRONTMATTER_RE.match(text)
    if not match:
        return None
    try:
        fm = yaml.safe_load(match.group(1))
    except yaml.YAMLError:
        return None
    if not isinstance(fm, dict):
        return None
    fm["_body"] = text[match.end():]
    fm["_path"] = str(path)
    return fm


def _page_id_from_subject(subject: str | None, kind: str, phase: str) -> str:
    """Derive a kebab-case page ID from subject, kind, and phase."""
    base = subject or f"{kind}-{phase}"
    slug = re.sub(r"[^a-z0-9]+", "-", base.lower().strip()).strip("-")
    return slug[:80] if slug else f"{kind}-{uuid4().hex[:8]}"


def _write_wiki_page(
    *,
    category: str,
    page_id: str,
    summary: str,
    body: str,
    tags: list[str],
    by: str,
    refs: list[str] | None = None,
    root: Path | None = None,
) -> Path:
    """Write or update a wiki page. Returns the page path."""
    cat_dir = wiki_root(root) / category
    cat_dir.mkdir(parents=True, exist_ok=True)
    page_path = cat_dir / f"{page_id}.md"

    now = timestamp_utc()
    rev = 1
    created = now

    existing = _parse_wiki_page(page_path)
    if existing:
        rev = int(existing.get("rev", 0)) + 1
        created = existing.get("created", now)

    frontmatter = {
        "id": page_id,
        "cat": category,
        "rev": rev,
        "created": created,
        "updated": now,
        "by": by,
        "tags": tags,
        "summary": summary,
        "status": "active",
    }
    if refs:
        frontmatter["refs"] = refs

    fm_text = yaml.safe_dump(frontmatter, sort_keys=False, default_flow_style=None)
    page_text = f"---\n{fm_text}---\n{body}\n"
    page_path.write_text(page_text, encoding="utf-8")
    return page_path


def _update_category_index(category: str, *, root: Path | None = None) -> None:
    """Rebuild a category _index.yaml from page frontmatter."""
    cat_dir = wiki_root(root) / category
    if not cat_dir.exists():
        return
    pages: list[dict[str, Any]] = []
    for md_file in sorted(cat_dir.glob("*.md")):
        fm = _parse_wiki_page(md_file)
        if fm and fm.get("status") != "archived":
            pages.append({
                "id": fm.get("id", md_file.stem),
                "summary": fm.get("summary", ""),
                "tags": fm.get("tags", []),
                "rev": fm.get("rev", 1),
                "updated": fm.get("updated", ""),
            })
    pages.sort(key=lambda p: p.get("updated", ""), reverse=True)
    index_data = {
        "category": category,
        "updated": timestamp_utc(),
        "pages": pages,
    }
    index_path = cat_dir / "_index.yaml"
    index_path.write_text(yaml.safe_dump(index_data, sort_keys=False), encoding="utf-8")


def _update_root_index(*, root: Path | None = None) -> None:
    """Rebuild the root wiki _index.yaml from all category indexes."""
    w_root = wiki_root(root)
    if not w_root.exists():
        return

    schema_path = w_root / "_schema.yaml"
    schema: dict[str, Any] = {}
    if schema_path.exists():
        schema = yaml.safe_load(schema_path.read_text(encoding="utf-8")) or {}

    categories_config = schema.get("categories", {})
    categories: dict[str, Any] = {}
    total_pages = 0

    for cat_dir in sorted(w_root.iterdir()):
        if not cat_dir.is_dir() or cat_dir.name.startswith("_"):
            continue
        cat_index = cat_dir / "_index.yaml"
        if cat_index.exists():
            cat_data = yaml.safe_load(cat_index.read_text(encoding="utf-8")) or {}
            pages = cat_data.get("pages", [])
        else:
            pages = []

        count = len(pages)
        total_pages += count
        purpose = (categories_config.get(cat_dir.name, {}) or {}).get("purpose", "")
        hot = pages[:3]
        categories[cat_dir.name] = {
            "count": count,
            "summary": purpose,
            "hot": hot,
        }

    root_index = {
        "version": 1,
        "updated": timestamp_utc(),
        "total_pages": total_pages,
        "categories": categories,
    }
    index_path = w_root / "_index.yaml"
    index_path.write_text(yaml.safe_dump(root_index, sort_keys=False), encoding="utf-8")


def _append_changelog(
    *,
    role: str,
    action: str,
    target: str,
    summary: str,
    root: Path | None = None,
) -> None:
    """Append an entry to the monthly changelog."""
    cl_root = changelog_root(root)
    cl_root.mkdir(parents=True, exist_ok=True)

    now = datetime.now(timezone.utc)
    month_file = cl_root / f"{now.strftime('%Y-%m')}.yaml"

    entry = {
        "ts": now.strftime("%Y-%m-%dT%H:%M:%S.%fZ"),
        "role": role,
        "action": action,
        "target": target,
        "summary": summary[:80],
    }

    entries: list[dict[str, Any]] = []
    if month_file.exists():
        existing = yaml.safe_load(month_file.read_text(encoding="utf-8"))
        if isinstance(existing, list):
            entries = existing
    entries.append(entry)
    month_file.write_text(yaml.safe_dump(entries, sort_keys=False), encoding="utf-8")


# ---------------------------------------------------------------------------
# Public API — wiki-backed
# ---------------------------------------------------------------------------

def write_wiki_page(
    *,
    kind: str,
    phase: str,
    summary: str,
    payload: dict[str, Any] | None = None,
    tags: list[str] | None = None,
    subject: str | None = None,
    source: str = DEFAULT_SOURCE,
    root: Path | None = None,
) -> Path:
    """Write a wiki page from structured knowledge. Returns the page path."""
    category = KIND_CATEGORY_MAP.get(kind, "context")
    page_id = _page_id_from_subject(subject, kind, phase)
    clean_tags = _clean_text_list(tags) + [kind, phase]
    clean_summary = _required_text(summary, "Wiki page summary must not be empty.")

    body_lines: list[str] = []
    if payload:
        for key, value in payload.items():
            if isinstance(value, list):
                body_lines.append(f"## {key}")
                for item in value:
                    body_lines.append(f"- {item}")
            else:
                body_lines.append(f"- {key}: {value}")
    body = "\n".join(body_lines)

    page_path = _write_wiki_page(
        category=category,
        page_id=page_id,
        summary=clean_summary,
        body=body,
        tags=clean_tags,
        by=source,
        root=root,
    )

    action = "updated" if page_path.exists() else "created"
    _update_category_index(category, root=root)
    _update_root_index(root=root)
    _append_changelog(
        role=source,
        action=action,
        target=f"wiki/{category}/{page_id}",
        summary=clean_summary,
        root=root,
    )

    return page_path


def query_wiki(
    *,
    category: str | None = None,
    tags: Iterable[str] | None = None,
    limit: int = 10,
    root: Path | None = None,
) -> list[dict[str, Any]]:
    """Query wiki pages by category and/or tags. Returns list of frontmatter dicts."""
    if limit <= 0:
        return []
    w_root = wiki_root(root)
    if not w_root.exists():
        return []

    tag_filter = _normalize_filter(tags)
    results: list[dict[str, Any]] = []

    dirs = [w_root / category] if category else sorted(w_root.iterdir())
    for cat_dir in dirs:
        if not cat_dir.is_dir() or cat_dir.name.startswith("_"):
            continue
        for md_file in sorted(cat_dir.glob("*.md"), reverse=True):
            fm = _parse_wiki_page(md_file)
            if not fm or fm.get("status") == "archived":
                continue
            if tag_filter and not tag_filter.issubset(set(fm.get("tags", []))):
                continue
            results.append(fm)
            if len(results) >= limit:
                return results
    return results


# ---------------------------------------------------------------------------
# Legacy API — kept for backward compatibility with orchestrator + tests
# ---------------------------------------------------------------------------

def append_memory_record(
    *,
    kind: str,
    phase: str,
    summary: str,
    payload: dict[str, Any] | None = None,
    tags: list[str] | None = None,
    artifact_refs: list[str] | None = None,
    supersedes: str | None = None,
    scope: str = DEFAULT_SCOPE,
    subject: str | None = None,
    source: str = DEFAULT_SOURCE,
    confidence: str = DEFAULT_CONFIDENCE,
    status: str = DEFAULT_STATUS,
    root: Path | None = None,
) -> Path:
    """Write a memory record as a wiki page. Legacy API wrapper."""
    # Validate inputs using original constraints
    _build_record(
        entry_id=uuid4().hex,
        timestamp=timestamp_utc(),
        kind=kind,
        phase=phase,
        scope=scope,
        subject=subject,
        source=source,
        confidence=confidence,
        status=status,
        tags=tags,
        summary=summary,
        artifact_refs=artifact_refs,
        payload=payload,
        supersedes=supersedes,
    )

    return write_wiki_page(
        kind=kind,
        phase=phase,
        summary=summary,
        payload=payload,
        tags=tags,
        subject=subject,
        source=source,
        root=root,
    )


def query_memory(
    *,
    phase: str | None = None,
    kind: str | Iterable[str] | None = None,
    scope: str | Iterable[str] | None = None,
    tags: Iterable[str] | None = None,
    subject: str | None = None,
    limit: int = 10,
    active_only: bool = True,
    include_superseded: bool = False,
    source: str | Iterable[str] | None = None,
    status: str | Iterable[str] | None = None,
    root: Path | None = None,
) -> list[dict[str, Any]]:
    """Query memory. Reads from wiki pages and legacy records."""
    if limit <= 0:
        return []

    # Determine category from kind
    category = None
    if isinstance(kind, str) and kind in KIND_CATEGORY_MAP:
        category = KIND_CATEGORY_MAP[kind]

    # Build combined tag filter
    combined_tags: list[str] = []
    if tags:
        combined_tags.extend(tags)
    if isinstance(kind, str):
        combined_tags.append(kind)
    if phase:
        combined_tags.append(phase)

    wiki_results = query_wiki(
        category=category,
        tags=combined_tags if combined_tags else None,
        limit=limit,
        root=root,
    )

    # Convert wiki frontmatter to legacy record format for compatibility
    results: list[dict[str, Any]] = []
    for fm in wiki_results:
        record = {
            "entry_id": fm.get("id", ""),
            "timestamp_utc": fm.get("updated", ""),
            "version": MEMORY_SCHEMA_VERSION,
            "kind": _extract_kind_from_tags(fm.get("tags", [])),
            "phase": _extract_phase_from_tags(fm.get("tags", [])),
            "scope": DEFAULT_SCOPE,
            "subject": fm.get("id"),
            "source": fm.get("by", DEFAULT_SOURCE),
            "confidence": DEFAULT_CONFIDENCE,
            "status": fm.get("status", DEFAULT_STATUS),
            "tags": fm.get("tags", []),
            "summary": fm.get("summary", ""),
            "artifact_refs": fm.get("refs", []),
            "payload": {},
        }
        if subject and record.get("subject") != subject:
            continue
        results.append(record)
        if len(results) >= limit:
            break

    # Fall back to legacy records if wiki has no results
    if not results:
        results = _load_legacy_records(
            phase=phase, kind=kind, scope=scope, tags=tags,
            subject=subject, limit=limit, active_only=active_only,
            include_superseded=include_superseded, source=source,
            status=status, root=root,
        )

    return results


def retrieve_memory(
    *,
    phase: str | None = None,
    kind: str | None = None,
    tags: set[str] | None = None,
    limit: int = 10,
    root: Path | None = None,
) -> list[dict[str, Any]]:
    return query_memory(phase=phase, kind=kind, tags=tags, limit=limit, root=root)


def latest_brief(
    *,
    phase: str | None = None,
    subject: str | None = None,
    scope: str | None = None,
    root: Path | None = None,
) -> dict[str, Any] | None:
    entries = query_memory(kind="phase-brief", phase=phase, subject=subject, scope=scope, limit=1, root=root)
    return entries[0] if entries else None


# ---------------------------------------------------------------------------
# Helpers
# ---------------------------------------------------------------------------

def _extract_kind_from_tags(tags: list[str]) -> str:
    for tag in tags:
        if tag in VALID_RECORD_KINDS:
            return tag
    return "fact"


def _extract_phase_from_tags(tags: list[str]) -> str:
    kind_tags = VALID_RECORD_KINDS | {"active", "stale", "archived"}
    for tag in tags:
        if tag not in kind_tags:
            return tag
    return "unknown"


def _load_legacy_records(
    *,
    phase: str | None = None,
    kind: str | Iterable[str] | None = None,
    scope: str | Iterable[str] | None = None,
    tags: Iterable[str] | None = None,
    subject: str | None = None,
    limit: int = 10,
    active_only: bool = True,
    include_superseded: bool = False,
    source: str | Iterable[str] | None = None,
    status: str | Iterable[str] | None = None,
    root: Path | None = None,
) -> list[dict[str, Any]]:
    """Load from legacy JSON records for backward compatibility."""
    entries = _load_records(root=root)
    kind_filter = _normalize_filter(kind)
    scope_filter = _normalize_filter(scope)
    tag_filter = _normalize_filter(tags)
    source_filter = _normalize_filter(source)
    status_filter = _normalize_filter(status)
    superseded_ids = _superseded_entry_ids(entries)

    results: list[dict[str, Any]] = []
    for entry in sorted(entries, key=_record_sort_key, reverse=True):
        if phase is not None and entry.get("phase") != phase:
            continue
        if kind_filter is not None and entry.get("kind") not in kind_filter:
            continue
        if scope_filter is not None and entry.get("scope") not in scope_filter:
            continue
        if subject is not None and entry.get("subject") != subject:
            continue
        if source_filter is not None and entry.get("source") not in source_filter:
            continue
        if status_filter is not None and entry.get("status") not in status_filter:
            continue
        if tag_filter is not None and not tag_filter.issubset(set(entry.get("tags", []))):
            continue
        if active_only and entry.get("status") != DEFAULT_STATUS:
            continue
        if not include_superseded and entry.get("status") == "superseded":
            continue
        if not include_superseded and _is_superseded(entry, superseded_ids):
            continue
        results.append(entry)
        if len(results) >= limit:
            break
    return results


def _load_records(*, root: Path | None = None) -> list[dict[str, Any]]:
    entries_by_id: dict[str, dict[str, Any]] = {}
    for path in _record_paths(root=root):
        record = _load_record(path)
        entries_by_id[record["entry_id"]] = record
    return list(entries_by_id.values())


def _record_paths(*, root: Path | None = None) -> list[Path]:
    paths: list[Path] = []
    seen: set[Path] = set()
    for base in [*legacy_records_roots(root), records_root(create=False, root=root)]:
        if base in seen:
            continue
        seen.add(base)
        if not base.exists():
            continue
        paths.extend(sorted(base.glob("*.json")))
    return paths


def _load_record(path: Path) -> dict[str, Any]:
    try:
        entry = json.loads(path.read_text(encoding="utf-8"))
    except json.JSONDecodeError as exc:
        raise ValueError(f"Invalid memory record {path}: {exc.msg}.") from exc
    if not isinstance(entry, dict):
        raise ValueError(f"Memory record {path} must contain a mapping.")
    return _normalize_loaded_record(entry, path).as_dict()


def _normalize_loaded_record(entry: dict[str, Any], path: Path) -> MemoryRecord:
    entry_id = _required_text(entry.get("entry_id"), f"Memory record {path} is missing 'entry_id'.")
    timestamp = _required_text(entry.get("timestamp_utc"), f"Memory record {path} is missing 'timestamp_utc'.")
    kind = _required_text(entry.get("kind"), f"Memory record {path} is missing 'kind'.")
    phase = _required_text(entry.get("phase"), f"Memory record {path} is missing 'phase'.")
    summary = _required_text(entry.get("summary"), f"Memory record {path} is missing 'summary'.")
    version = int(entry.get("version", 1))
    return _build_record(
        entry_id=entry_id,
        timestamp=timestamp,
        kind=kind,
        phase=phase,
        scope=_clean_optional_text(entry.get("scope")) or DEFAULT_SCOPE,
        subject=_clean_optional_text(entry.get("subject")),
        source=_clean_optional_text(entry.get("source")) or DEFAULT_SOURCE,
        confidence=_clean_optional_text(entry.get("confidence")) or DEFAULT_CONFIDENCE,
        status=_clean_optional_text(entry.get("status")) or DEFAULT_STATUS,
        tags=entry.get("tags"),
        summary=summary,
        artifact_refs=entry.get("artifact_refs"),
        payload=entry.get("payload"),
        supersedes=_clean_optional_text(entry.get("supersedes")),
        version=version,
    )


def _build_record(
    *,
    entry_id: str,
    timestamp: str,
    kind: str,
    phase: str,
    scope: str,
    subject: str | None,
    source: str,
    confidence: str,
    status: str,
    tags: Iterable[str] | None,
    summary: str,
    artifact_refs: Iterable[str] | None,
    payload: Any,
    supersedes: str | None,
    version: int = MEMORY_SCHEMA_VERSION,
) -> MemoryRecord:
    clean_kind = _required_text(kind, "Memory record kind must not be empty.")
    if clean_kind not in VALID_RECORD_KINDS:
        raise ValueError(f"Unsupported memory record kind '{clean_kind}'.")

    clean_phase = _required_text(phase, "Memory record phase must not be empty.")
    clean_summary = _required_text(summary, "Memory record summary must not be empty.")
    clean_scope = _required_text(scope, "Memory record scope must not be empty.")
    clean_source = _required_text(source, "Memory record source must not be empty.")
    clean_confidence = _required_text(confidence, "Memory record confidence must not be empty.")
    clean_status = _required_text(status, "Memory record status must not be empty.")
    if clean_confidence not in VALID_CONFIDENCE_LEVELS:
        raise ValueError(f"Unsupported memory confidence '{clean_confidence}'.")
    if clean_status not in VALID_RECORD_STATUSES:
        raise ValueError(f"Unsupported memory record status '{clean_status}'.")
    if payload is None:
        clean_payload: dict[str, Any] = {}
    elif isinstance(payload, dict):
        clean_payload = payload
    else:
        raise ValueError("Memory record payload must be a mapping.")

    return MemoryRecord(
        entry_id=_required_text(entry_id, "Memory record entry_id must not be empty."),
        timestamp_utc=_required_text(timestamp, "Memory record timestamp_utc must not be empty."),
        version=version,
        kind=clean_kind,
        phase=clean_phase,
        scope=clean_scope,
        subject=_clean_optional_text(subject),
        source=clean_source,
        confidence=clean_confidence,
        status=clean_status,
        tags=_clean_text_list(tags),
        summary=clean_summary,
        artifact_refs=_clean_text_list(artifact_refs),
        payload=clean_payload,
        supersedes=_clean_optional_text(supersedes),
    )


def _record_sort_key(entry: dict[str, Any]) -> tuple[datetime, str]:
    return (_parse_timestamp(entry.get("timestamp_utc")), str(entry.get("entry_id", "")))


def _parse_timestamp(value: Any) -> datetime:
    cleaned = _clean_optional_text(value)
    if cleaned is None:
        return datetime.min.replace(tzinfo=timezone.utc)
    normalized = f"{cleaned[:-1]}+00:00" if cleaned.endswith("Z") else cleaned
    try:
        parsed = datetime.fromisoformat(normalized)
    except ValueError:
        return datetime.min.replace(tzinfo=timezone.utc)
    if parsed.tzinfo is None:
        return parsed.replace(tzinfo=timezone.utc)
    return parsed.astimezone(timezone.utc)


def _normalize_filter(value: str | Iterable[str] | None) -> set[str] | None:
    if value is None:
        return None
    if isinstance(value, str):
        cleaned = value.strip()
        return {cleaned} if cleaned else set()
    cleaned_values = {str(item).strip() for item in value if str(item).strip()}
    return cleaned_values if cleaned_values else set()


def _clean_text_list(values: Iterable[str] | None) -> list[str]:
    if values is None:
        return []
    return [str(item).strip() for item in values if str(item).strip()]


def _clean_optional_text(value: Any) -> str | None:
    if value is None:
        return None
    cleaned = str(value).strip()
    return cleaned if cleaned else None


def _required_text(value: Any, message: str) -> str:
    cleaned = _clean_optional_text(value)
    if cleaned is None:
        raise ValueError(message)
    return cleaned


def _superseded_entry_ids(entries: list[dict[str, Any]]) -> set[str]:
    superseded_ids: set[str] = set()
    for entry in entries:
        supersedes = entry.get("supersedes")
        if isinstance(supersedes, str) and supersedes.strip():
            superseded_ids.add(supersedes.strip())
    return superseded_ids


def _is_superseded(entry: dict[str, Any], superseded_ids: set[str]) -> bool:
    entry_id = entry.get("entry_id")
    return isinstance(entry_id, str) and entry_id in superseded_ids


def _ensure_supersedes_target_exists(entry_id: str, *, root: Path | None = None) -> None:
    if any(record.get("entry_id") == entry_id for record in _load_records(root=root)):
        return
    raise ValueError(f"Memory record supersedes unknown entry '{entry_id}'.")
