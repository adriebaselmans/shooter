---
name: wiki-write
description: Create or update wiki pages to persist reusable project knowledge, maintain category and root indexes, and append a changelog entry for every write.
---

# Wiki Write

Use this skill after completing phase work that produced reusable knowledge — facts, decisions, patterns, conventions, or lessons that future runs should know.

## Goals
- Persist knowledge as living wiki pages, not append-only logs.
- Keep indexes current so wiki-read is always efficient.
- Maintain a lightweight changelog for audit.

## Required Inputs
- The knowledge to persist (fact, decision, convention, lesson, etc.)
- `.ai-team/framework/memory/wiki/_schema.yaml` (category registry)
- The existing page if updating (check before creating)

## Required Outputs
- Created or updated wiki page at `wiki/<category>/<page-id>.md`
- Updated `wiki/<category>/_index.yaml`
- Updated `wiki/_index.yaml`
- Appended entry in `memory/changelog/<YYYY-MM>.yaml`

## Page Format

Every wiki page is a markdown file with YAML frontmatter:

```yaml
---
id: <kebab-case-identifier>
cat: <category-name>
rev: <integer, starts at 1, increment on every update>
created: <ISO-8601 UTC timestamp of first creation>
updated: <ISO-8601 UTC timestamp of latest update>
by: <role that last wrote this page>
tags: [<relevant, searchable, keywords>]
summary: "<one-line description, max 120 chars>"
refs: [<category/page-id>, ...]
status: active
---
<terse markdown body>
```

### Body format rules
- Use terse, structured content optimized for AI consumption.
- Prefer key-value pairs, short bullet lists, and small headers.
- No prose paragraphs. No filler. No formatting for humans.
- Include an `## open` section at the bottom for unresolved questions.
- Target: a page should fit in ~200 tokens. Split into multiple pages if larger.

## Procedure

### Step 1 — Classify the knowledge
Determine which category fits. Read `_schema.yaml` for the category registry with purpose descriptions.

### Step 2 — Check for existing page
Search the category for an existing page on this topic:
- Read `<category>/_index.yaml` and scan summaries
- If a page already covers this topic → update it (increment `rev`, update `updated` and `by`, merge content)
- If no page exists → create a new one

### Step 3 — Choose page ID
Use `kebab-case` identifiers that describe the topic:
- Good: `stack-choices`, `error-handling-pattern`, `auth-flow`, `orm-decision`
- Bad: `page1`, `update-2026-04`, `misc-notes`

### Step 4 — Write the page
- New page: create `wiki/<category>/<page-id>.md` with `rev: 1` and `created` = `updated` = now
- Existing page: edit the file, increment `rev`, set `updated` = now, set `by` = current role
- When updating: replace stale content, don't append. The page reflects current truth, not history.

### Step 5 — Update category index
Rewrite `wiki/<category>/_index.yaml` by scanning all `.md` files in the category directory and reading their frontmatter. Format:

```yaml
category: <name>
updated: <now>
pages:
  - id: <page-id>
    summary: "<from frontmatter>"
    tags: [<from frontmatter>]
    rev: <from frontmatter>
    updated: <from frontmatter>
```

Sort pages by `updated` descending (most recent first).

### Step 6 — Update root index
Rewrite `wiki/_index.yaml` by reading all category `_index.yaml` files. Format:

```yaml
version: 1
updated: <now>
total_pages: <count across all categories>
categories:
  <category-name>:
    count: <number of pages>
    summary: "<from _schema.yaml purpose>"
    hot:
      - id: <most recent page>
        summary: "<from frontmatter>"
        tags: [<from frontmatter>]
        rev: <from frontmatter>
      # up to 3 most recent pages per category
```

### Step 7 — Append changelog entry
Open or create `memory/changelog/<YYYY-MM>.yaml` (based on current month). Append:

```yaml
- ts: <ISO-8601 UTC>
  role: <current role>
  action: <created|updated|archived>
  target: wiki/<category>/<page-id>
  summary: "<what changed, max 80 chars>"
```

## Creating a New Category

If no existing category fits:
1. Check `_schema.yaml` one more time — prefer an existing category over creating a new one.
2. Create the directory `wiki/<new-category>/`
3. Add the category to `_schema.yaml` with a `purpose` field
4. Create `wiki/<new-category>/_index.yaml` (empty pages list)
5. Proceed with page creation as normal

## Updating vs Creating Decision

- **Update** when: the same topic already has a page (even if the content has changed significantly). Pages are living documents.
- **Create** when: the topic is genuinely new and not covered by any existing page.
- **Split** when: a page has grown beyond ~200 tokens and covers multiple distinct topics.
- **Archive** when: a page is no longer relevant. Set `status: archived` in frontmatter. Keep the file but exclude from indexes.

## Parallel Write Safety
- Different pages are different files — safe to write in parallel.
- If you cannot update a category index because another process may be writing to the same category, write your page file only and note that the index needs rebuilding. The next wiki-read or wiki-write will detect the staleness and rebuild.
- Indexes are always rebuildable from page frontmatter — they are caches, not sources of truth.

## What to Write and What Not to Write

### Write to wiki
- Stack and technology choices (→ `architecture/`)
- Design decisions with rationale (→ `decisions/`)
- Coding patterns and conventions adopted (→ `conventions/`)
- Domain terms, business rules, glossary (→ `domain/`)
- Repository analysis results (→ `repositories/`)
- Bug root causes and lessons learned (→ `incidents/`)
- Environment constraints, deployment facts (→ `context/`)
- Project scope, goals, stakeholders (→ `project/`)

### Do NOT write to wiki
- Current-run orchestration state (that's `state.json`)
- Phase artifacts (that's `doc_templates/*/current.yaml`)
- Raw logs, traces, or verbose command output
- Temporary investigation notes
- Anything that is only relevant to the current run, not future runs

## Rules
- Every page must have valid YAML frontmatter with all required fields.
- Every write must be followed by index updates (steps 5-6) and a changelog entry (step 7).
- Never delete a page file. Archive by setting `status: archived`.
- Keep pages terse. If you're writing prose, you're writing too much.
- Tags should be lowercase, hyphenated, and reusable across pages.
