"""Validate wiki structure, page format, and index consistency."""

from __future__ import annotations

from pathlib import Path
from typing import Any

import yaml

try:
    from .memory_store import _parse_wiki_page, wiki_root
except ImportError:  # pragma: no cover
    from memory_store import _parse_wiki_page, wiki_root

REQUIRED_FRONTMATTER = {"id", "cat", "rev", "created", "updated", "by", "tags", "summary", "status"}
VALID_STATUSES = {"active", "stale", "archived"}


def validate_wiki(root: Path | None = None) -> list[str]:
    """Return a list of validation error messages. Empty list means valid."""
    errors: list[str] = []
    w_root = wiki_root(root)

    if not w_root.exists():
        return ["Wiki directory does not exist."]

    schema_path = w_root / "_schema.yaml"
    if not schema_path.exists():
        errors.append("Missing _schema.yaml")
    else:
        errors.extend(_validate_schema(schema_path))

    root_index_path = w_root / "_index.yaml"
    if not root_index_path.exists():
        errors.append("Missing root _index.yaml")

    for cat_dir in sorted(w_root.iterdir()):
        if not cat_dir.is_dir() or cat_dir.name.startswith("_"):
            continue
        errors.extend(_validate_category(cat_dir))

    return errors


def _validate_schema(path: Path) -> list[str]:
    errors: list[str] = []
    try:
        data = yaml.safe_load(path.read_text(encoding="utf-8"))
    except yaml.YAMLError as exc:
        return [f"Invalid YAML in _schema.yaml: {exc}"]

    if not isinstance(data, dict):
        return ["_schema.yaml must be a mapping"]

    categories = data.get("categories")
    if not isinstance(categories, dict):
        errors.append("_schema.yaml must have a 'categories' mapping")
    else:
        for cat_name, cat_data in categories.items():
            if not isinstance(cat_data, dict) or "purpose" not in cat_data:
                errors.append(f"Category '{cat_name}' missing 'purpose' in _schema.yaml")

    return errors


def _validate_category(cat_dir: Path) -> list[str]:
    errors: list[str] = []
    cat_name = cat_dir.name

    cat_index = cat_dir / "_index.yaml"
    if not cat_index.exists():
        errors.append(f"Category '{cat_name}' missing _index.yaml")

    for md_file in sorted(cat_dir.glob("*.md")):
        errors.extend(_validate_page(md_file, cat_name))

    return errors


def _validate_page(path: Path, expected_category: str) -> list[str]:
    errors: list[str] = []
    fm = _parse_wiki_page(path)

    if fm is None:
        errors.append(f"Page {path.name} in {expected_category}: invalid or missing frontmatter")
        return errors

    missing = REQUIRED_FRONTMATTER - set(fm.keys())
    if missing:
        errors.append(f"Page {path.name} in {expected_category}: missing frontmatter fields: {missing}")

    if fm.get("cat") != expected_category:
        errors.append(f"Page {path.name}: cat='{fm.get('cat')}' but in category dir '{expected_category}'")

    status = fm.get("status")
    if status and status not in VALID_STATUSES:
        errors.append(f"Page {path.name} in {expected_category}: invalid status '{status}'")

    if not isinstance(fm.get("tags"), list):
        errors.append(f"Page {path.name} in {expected_category}: tags must be a list")

    rev = fm.get("rev")
    if not isinstance(rev, int) or rev < 1:
        errors.append(f"Page {path.name} in {expected_category}: rev must be a positive integer")

    return errors
