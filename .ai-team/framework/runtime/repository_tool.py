from __future__ import annotations

from typing import Any


def repository_exploration_request(target_path: str, objective: str) -> dict[str, Any]:
    return {
        "tool": "repository-exploration",
        "target_path": target_path,
        "objective": objective,
        "expected_outputs": [
            "repository_brief",
            "key_risks",
            "extension_points",
        ],
        "memory_tags": ["repository-knowledge", "exploration"],
        "wiki_category": "repositories",
    }
