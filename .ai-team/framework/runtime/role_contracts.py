from __future__ import annotations

from pathlib import Path
from typing import Any

from jsonschema import Draft202012Validator

try:
    from .spec_loader import load_role_output_contracts as load_role_output_contracts_spec
    from .spec_loader import load_yaml
except ImportError:  # pragma: no cover - compatibility for direct script-style imports
    from spec_loader import load_role_output_contracts as load_role_output_contracts_spec
    from spec_loader import load_yaml


def load_role_output_contracts(path: str | Path | None = None) -> dict[str, Any]:
    if path is None:
        return load_role_output_contracts_spec()
    return load_yaml(Path(path))


def load_role_output_schema(role_key: str, path: str | Path | None = None) -> dict[str, Any]:
    contracts = load_role_output_contracts(path)
    roles = contracts.get("roles", {})
    if not isinstance(roles, dict):
        raise ValueError("Role output contracts file must contain a 'roles' mapping.")
    try:
        schema = roles[role_key]
    except KeyError as exc:
        raise KeyError(f"Missing role output schema for '{role_key}'.") from exc
    if not isinstance(schema, dict):
        raise ValueError(f"Role output schema for '{role_key}' must be a mapping.")

    materialized = dict(schema)
    materialized["$schema"] = "https://json-schema.org/draft/2020-12/schema"
    materialized["definitions"] = dict(contracts.get("definitions", {}))
    return materialized


def validate_role_output(role_key: str, update: dict[str, Any], path: str | Path | None = None) -> list[str]:
    schema = load_role_output_schema(role_key, path)
    validator = Draft202012Validator(schema)
    return [error.message for error in validator.iter_errors(update)]


def validate_role_output_coverage(role_keys: list[str], path: str | Path | None = None) -> None:
    contracts = load_role_output_contracts(path)
    roles = contracts.get("roles", {})
    if not isinstance(roles, dict):
        raise ValueError("Role output contracts file must contain a 'roles' mapping.")
    missing = sorted(set(role_keys) - set(roles))
    if missing:
        raise KeyError(f"Missing role output schemas for roles: {', '.join(missing)}")
