from __future__ import annotations

from pathlib import Path
from typing import Any

import yaml

try:
    from .spec_loader import load_artifact_schema, repo_root
except ImportError:  # pragma: no cover - compatibility for direct script-style imports
    from spec_loader import load_artifact_schema, repo_root


ARTIFACT_FILES = {
    "requirements": "doc_templates/requirements/current.yaml",
    "design": "doc_templates/design/current.yaml",
    "review": "doc_templates/review/current.yaml",
    "dod": "doc_templates/dod/current.yaml",
}


def artifact_path(name: str) -> Path:
    try:
        rel_path = ARTIFACT_FILES[name]
    except KeyError as exc:
        raise KeyError(f"Unknown artifact: {name}") from exc
    return repo_root() / rel_path


def load_artifact(name: str) -> dict[str, Any]:
    yaml_path = artifact_path(name)
    with yaml_path.open("r", encoding="utf-8") as handle:
        data = yaml.safe_load(handle) or {}
    if not isinstance(data, dict):
        raise ValueError(f"Expected mapping in {yaml_path}")
    return data


def save_artifact(name: str, data: dict[str, Any]) -> None:
    validate_artifact_data(name, data)
    artifact_path(name).write_text(yaml.safe_dump(data, sort_keys=False), encoding="utf-8")


def artifact_summary(name: str, data: dict[str, Any] | None = None) -> dict[str, Any]:
    payload = data or load_artifact(name)
    schema = load_artifact_schema(name)
    summary_fields = schema.get("summary_fields", [])
    return {field: payload.get(field) for field in summary_fields if field in payload}


def validate_artifact_data(name: str, data: dict[str, Any]) -> list[str]:
    schema = load_artifact_schema(name)
    messages: list[str] = []
    required_fields = schema.get("required_fields", [])
    for field in required_fields:
        if field not in data:
            messages.append(f"{name} artifact is missing '{field}'.")
            continue
        value = data[field]
        if value in ("", None):
            messages.append(f"{name} artifact field '{field}' is empty.")

    field_types = schema.get("field_types", {})
    for field, expected in field_types.items():
        if field not in data:
            continue
        value = data[field]
        if expected == "string" and not isinstance(value, str):
            messages.append(f"{name} artifact field '{field}' must be a string.")
        if expected == "list" and not isinstance(value, list):
            messages.append(f"{name} artifact field '{field}' must be a list.")
    return messages
