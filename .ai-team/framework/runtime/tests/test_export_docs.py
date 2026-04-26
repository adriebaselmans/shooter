from __future__ import annotations

import sys
import unittest
from pathlib import Path
from unittest.mock import patch

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))

import export_docs


class ExportDocsTests(unittest.TestCase):
    def test_render_artifact_doc_uses_schema_section_order_and_headings(self) -> None:
        schema = {
            "artifact_type": "design",
            "title": "Current Design",
            "markdown_sections": [
                {"heading": "Title", "field": "title"},
                {"heading": "Design Goal", "field": "design_goal"},
            ],
        }
        payload = {"title": "Example Design", "design_goal": "Do the thing."}

        with (
            patch.object(export_docs, "load_artifact_schema", return_value=schema),
            patch.object(export_docs, "load_artifact", return_value=payload),
        ):
            rendered = export_docs.render_artifact_doc("design")

        self.assertLess(rendered.index("## Title"), rendered.index("## Design Goal"))
        self.assertIn("## Title\n- Example Design", rendered)
        self.assertIn("## Design Goal\n- Do the thing.", rendered)

    def test_render_artifact_doc_requires_markdown_sections(self) -> None:
        schema = {
            "artifact_type": "requirements",
            "title": "Current Requirements",
            "required_fields": ["status"],
        }

        with (
            patch.object(export_docs, "load_artifact_schema", return_value=schema),
            patch.object(export_docs, "load_artifact", return_value={"status": "Ready."}),
        ):
            with self.assertRaisesRegex(ValueError, "missing markdown_sections"):
                export_docs.render_artifact_doc("requirements")


if __name__ == "__main__":
    unittest.main()

