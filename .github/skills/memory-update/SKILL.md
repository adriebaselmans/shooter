---
name: memory-update
description: Update the project wiki after each completed phase by writing or updating relevant wiki pages using the wiki-write skill.
---

# Memory Update

Use this skill when acting as the coordinator and a phase has been completed.

## Goals
- Preserve useful project knowledge as wiki pages for future iterations.
- Keep the wiki current and navigable.
- Avoid duplicating active shared state or phase artifacts.

## Required Inputs
- Reusable knowledge produced by the current run
- Bootstrapped project metadata in `.ai-team/framework/init-metadata.json`
- Existing wiki in `.ai-team/framework/memory/wiki/`

## Required Outputs
- Updated wiki pages via the `wiki-write` skill
- Updated wiki indexes (category and root)
- Changelog entries for each write

## Rules
- Use the `wiki-write` skill for all memory writes. Do not write to `records/` or standalone markdown files.
- Wiki pages are living documents — update existing pages on the same topic, do not create duplicates.
- Keep entries short, factual, reusable, and cross-run valuable.
- Write wiki pages only in bootstrapped project repos created from this skeleton.
- Do not copy current-run orchestration state, trace, or phase artifacts into wiki pages.
