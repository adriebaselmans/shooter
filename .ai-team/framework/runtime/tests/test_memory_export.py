from __future__ import annotations

import sys
import unittest
from pathlib import Path
from unittest.mock import patch

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))

import memory_export


class MemoryExportTests(unittest.TestCase):
    def test_render_project_log_snapshot_uses_wiki_pages(self) -> None:
        with patch.object(
            memory_export,
            'query_wiki',
            return_value=[
                {
                    'updated': '2026-03-29T10:00:00.000000Z',
                    'cat': 'project',
                    'summary': 'Compact brief for requirements',
                    'tags': ['phase-brief', 'requirements'],
                }
            ],
        ) as query_wiki_mock:
            markdown = memory_export.render_project_log_snapshot(limit=5)

        self.assertIn('# Project Log Snapshot', markdown)
        self.assertIn('Compact brief for requirements', markdown)
        query_wiki_mock.assert_called_once_with(
            category='project',
            limit=5,
            root=None,
        )

    def test_render_known_context_snapshot_groups_context_and_decisions(self) -> None:
        with patch.object(
            memory_export,
            'query_wiki',
            side_effect=[
                [{'updated': '2026-04-01T10:00:00Z', 'summary': 'The runtime is local-first', 'tags': ['context']}],
                [{'updated': '2026-04-01T10:00:00Z', 'summary': 'Keep records in git', 'tags': ['decision']}],
            ],
        ):
            markdown = memory_export.render_known_context_snapshot(fact_limit=3, decision_limit=2)

        self.assertIn('## Active Context', markdown)
        self.assertIn('The runtime is local-first', markdown)
        self.assertIn('## Active Decisions', markdown)
        self.assertIn('Keep records in git', markdown)

    def test_render_memory_snapshot_raises_on_unknown_view(self) -> None:
        with self.assertRaisesRegex(ValueError, "Unknown memory snapshot view 'invalid'"):
            memory_export.render_memory_snapshot('invalid')


if __name__ == '__main__':
    unittest.main()
