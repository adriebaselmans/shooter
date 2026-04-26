---
name: wiki-read
description: Navigate and retrieve relevant knowledge from the project wiki without reading everything. Read the root index first, then drill into only the categories and pages that matter for the current task.
---

# Wiki Read

Use this skill at the start of any phase to ground your work in existing project knowledge.

## Goals
- Find relevant knowledge fast without loading the entire wiki.
- Avoid rediscovering what the team already knows.
- Surface cross-references to related topics.

## Required Inputs
- The current task or phase objective (to know what's relevant)
- `.ai-team/framework/memory/wiki/_index.yaml`

## Required Outputs
- The set of wiki pages read, cited by path
- Any knowledge gaps discovered (topics that should exist but don't)

## Procedure

### Step 1 — Read the root index
Read `.ai-team/framework/memory/wiki/_index.yaml`. This file lists every category with a summary and the most recently updated pages per category. This is your map.

### Step 2 — Identify relevant categories
Match your current task against category summaries. Typical mappings:
- Starting a new feature → `project/`, `domain/`, `decisions/`
- Designing architecture → `architecture/`, `conventions/`, `repositories/`
- Implementing code → `architecture/`, `conventions/`, `incidents/`
- Reviewing code → `conventions/`, `decisions/`, `incidents/`
- Testing → `domain/`, `conventions/`, `incidents/`
- Exploring a repo → `repositories/`

### Step 3 — Read category index
For each relevant category, read `<category>/_index.yaml`. This lists all pages in that category with their summary and tags. Use summaries and tags to decide which pages to read.

### Step 4 — Read only needed pages
Open only the pages whose summary or tags indicate relevance. Each page has a `refs:` field in its frontmatter pointing to related pages — follow refs if the cross-linked topic matters.

### Step 5 — Report gaps
If your task needs knowledge that doesn't exist in the wiki yet, note this as a gap. After completing your phase work, use wiki-write to fill it.

## Rules
- Never read all pages in a category unless the category has fewer than 5 pages and all are relevant.
- Prefer the root index over directory scanning. Fall back to `glob` + frontmatter `grep` only if `_index.yaml` is missing or clearly stale.
- If `_index.yaml` is missing, rebuild it: scan category directories, read each page's YAML frontmatter, assemble the index, and write it. Then proceed normally.
- Do not modify page content during a read operation.
- Cite wiki page paths in your outputs so downstream roles can trace your reasoning.

## Index Staleness Detection
The root `_index.yaml` has an `updated` timestamp. If it is older than the most recent page you encounter (check file modification or frontmatter `updated`), treat the index as stale and rebuild it before relying on it.

## Fallback: No Wiki Yet
If `.ai-team/framework/memory/wiki/` does not exist or has no pages, the project has no wiki knowledge yet. Proceed without wiki context and note that wiki-write should initialize the wiki after this phase produces reusable knowledge.
