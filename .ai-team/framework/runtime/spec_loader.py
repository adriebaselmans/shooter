from __future__ import annotations

from pathlib import Path
from typing import Any

import yaml


def repo_root() -> Path:
    return Path(__file__).resolve().parents[3]


def ai_team_root() -> Path:
    return repo_root() / '.ai-team'


def runtime_dir() -> Path:
    return Path(__file__).resolve().parent


def config_dir() -> Path:
    return ai_team_root() / 'framework' / 'config'


def schemas_dir() -> Path:
    return ai_team_root() / 'framework' / 'schemas'


def docs_dir() -> Path:
    return repo_root() / 'docs'


def load_yaml(path: Path) -> dict[str, Any]:
    with path.open('r', encoding='utf-8') as handle:
        data = yaml.safe_load(handle) or {}
    if not isinstance(data, dict):
        raise ValueError(f'Expected mapping in {path}')
    return data


def load_team_spec() -> dict[str, Any]:
    return load_yaml(runtime_dir() / 'team.yaml')

def load_runtimes_config() -> dict[str, Any]:
    return load_yaml(config_dir() / 'runtimes.yaml')


def load_artifact_schema(name: str) -> dict[str, Any]:
    return load_yaml(schemas_dir() / f'{name}.schema.yaml')


def load_role_output_contracts() -> dict[str, Any]:
    return load_yaml(schemas_dir() / 'role_outputs.schema.yaml')
