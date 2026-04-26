from __future__ import annotations

import json
import shutil
import unittest
from pathlib import Path
from unittest.mock import patch
from uuid import uuid4

import sys

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))

import memory_store


class MemoryStoreWikiTests(unittest.TestCase):
    def setUp(self) -> None:
        sandbox_base = Path(__file__).resolve().parents[3] / '.tmp-pytest-workspace' / 'runtime-memory'
        sandbox_base.mkdir(parents=True, exist_ok=True)
        self._sandbox_root = sandbox_base / uuid4().hex
        self._sandbox_root.mkdir(parents=True, exist_ok=False)

    def tearDown(self) -> None:
        shutil.rmtree(self._sandbox_root, ignore_errors=True)

    def test_write_wiki_page_creates_page_with_frontmatter(self) -> None:
        root = self._sandbox_root / 'case-wiki-write'
        with patch.object(memory_store, 'repo_root', return_value=root):
            page_path = memory_store.write_wiki_page(
                kind='decision',
                phase='architecture',
                summary='Use FastAPI for web layer',
                payload={'framework': 'FastAPI', 'version': '0.111'},
                tags=['python', 'web'],
                subject='fastapi-choice',
                source='architect',
                root=root,
            )

        self.assertTrue(page_path.exists())
        content = page_path.read_text(encoding='utf-8')
        self.assertIn('summary: Use FastAPI for web layer', content)
        self.assertIn('cat: decisions', content)
        self.assertIn('rev: 1', content)
        self.assertIn('by: architect', content)

    def test_write_wiki_page_updates_indexes(self) -> None:
        root = self._sandbox_root / 'case-wiki-index'
        with patch.object(memory_store, 'repo_root', return_value=root):
            memory_store.write_wiki_page(
                kind='fact',
                phase='development',
                summary='Python 3.12 is the runtime',
                tags=['python'],
                source='developer',
                root=root,
            )

        cat_index = memory_store.wiki_root(root) / 'context' / '_index.yaml'
        self.assertTrue(cat_index.exists())
        import yaml
        cat_data = yaml.safe_load(cat_index.read_text(encoding='utf-8'))
        self.assertEqual(cat_data['category'], 'context')
        self.assertGreater(len(cat_data['pages']), 0)

        root_index = memory_store.wiki_root(root) / '_index.yaml'
        self.assertTrue(root_index.exists())
        root_data = yaml.safe_load(root_index.read_text(encoding='utf-8'))
        self.assertGreater(root_data['total_pages'], 0)

    def test_write_wiki_page_appends_changelog(self) -> None:
        root = self._sandbox_root / 'case-wiki-changelog'
        with patch.object(memory_store, 'repo_root', return_value=root):
            memory_store.write_wiki_page(
                kind='decision',
                phase='architecture',
                summary='Chose PostgreSQL',
                tags=['database'],
                source='architect',
                root=root,
            )

        import yaml
        changelog_dir = memory_store.changelog_root(root)
        yaml_files = list(changelog_dir.glob('*.yaml'))
        self.assertEqual(len(yaml_files), 1)
        entries = yaml.safe_load(yaml_files[0].read_text(encoding='utf-8'))
        self.assertEqual(len(entries), 1)
        self.assertIn('Chose PostgreSQL', entries[0]['summary'])

    def test_write_wiki_page_increments_rev_on_update(self) -> None:
        root = self._sandbox_root / 'case-wiki-update'
        with patch.object(memory_store, 'repo_root', return_value=root):
            path1 = memory_store.write_wiki_page(
                kind='decision',
                phase='architecture',
                summary='Initial decision',
                tags=['test'],
                subject='same-topic',
                source='architect',
                root=root,
            )
            path2 = memory_store.write_wiki_page(
                kind='decision',
                phase='architecture',
                summary='Updated decision',
                tags=['test'],
                subject='same-topic',
                source='architect',
                root=root,
            )

        self.assertEqual(path1, path2)
        fm = memory_store._parse_wiki_page(path2)
        self.assertEqual(fm['rev'], 2)
        self.assertIn('Updated decision', fm['summary'])

    def test_query_wiki_returns_pages_by_category(self) -> None:
        root = self._sandbox_root / 'case-wiki-query'
        with patch.object(memory_store, 'repo_root', return_value=root):
            memory_store.write_wiki_page(
                kind='decision', phase='architecture',
                summary='Decision A', tags=['arch'], source='architect', root=root,
            )
            memory_store.write_wiki_page(
                kind='fact', phase='development',
                summary='Fact B', tags=['dev'], source='developer', root=root,
            )

            decisions = memory_store.query_wiki(category='decisions', root=root)
            context = memory_store.query_wiki(category='context', root=root)

        self.assertEqual(len(decisions), 1)
        self.assertEqual(decisions[0]['summary'], 'Decision A')
        self.assertEqual(len(context), 1)
        self.assertEqual(context[0]['summary'], 'Fact B')

    def test_append_memory_record_legacy_api_writes_wiki_page(self) -> None:
        root = self._sandbox_root / 'case-legacy-api'
        with patch.object(memory_store, 'repo_root', return_value=root):
            page_path = memory_store.append_memory_record(
                kind='fact',
                phase='development',
                summary='Legacy API still works',
                tags=['compat'],
                root=root,
            )

        self.assertTrue(page_path.exists())
        self.assertIn('wiki', str(page_path))

    def test_append_memory_record_rejects_unsupported_kind(self) -> None:
        root = self._sandbox_root / 'case-invalid-kind'
        with patch.object(memory_store, 'repo_root', return_value=root):
            with self.assertRaisesRegex(ValueError, "Unsupported memory record kind 'note'"):
                memory_store.append_memory_record(
                    kind='note',
                    phase='development',
                    summary='Invalid kind',
                )

    def test_query_memory_reads_legacy_json_records_as_fallback(self) -> None:
        root = self._sandbox_root / 'case-legacy-fallback'
        legacy_root = root / 'framework' / 'memory' / 'logs'
        legacy_root.mkdir(parents=True, exist_ok=True)
        legacy_record = {
            'entry_id': 'legacy-1',
            'timestamp_utc': '2026-03-29T09:00:00.000000Z',
            'version': 2,
            'kind': 'decision',
            'phase': 'architecture',
            'scope': 'project',
            'source': 'architect',
            'confidence': 'high',
            'status': 'active',
            'tags': ['memory'],
            'summary': 'Keep local structured memory',
            'artifact_refs': [],
            'payload': {'decision': 'Use local records only'},
        }
        (legacy_root / 'legacy-1.json').write_text(json.dumps(legacy_record), encoding='utf-8')

        with patch.object(memory_store, 'repo_root', return_value=root):
            entries = memory_store.query_memory(kind='decision', scope='project', root=root)

        self.assertEqual(len(entries), 1)
        self.assertEqual(entries[0]['entry_id'], 'legacy-1')
        self.assertEqual(entries[0]['summary'], 'Keep local structured memory')


if __name__ == '__main__':
    unittest.main()
