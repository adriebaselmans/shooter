---
name: external-research
description: Retrieve current external evidence for freshness-sensitive technical decisions and return a concise, source-backed brief.
---

# External Research

Use this skill when acting as the scout role in this repository.

## Goals
- Find current evidence that could materially change a design or implementation decision.
- Prefer primary sources over secondary summaries.
- Produce a short brief that another role can use immediately.

## Required Inputs
- Bounded research question
- Why the answer matters
- Relevant local context when available

## Required Output
- A concise research brief with verified facts, source links, dates or versions, implications, confidence, and remaining unknowns

## Procedure
1. Restate the research question in one sentence.
2. Search the strongest relevant sources first.
3. Prefer official docs, API docs, release notes, standards, vendor pages, advisories, and papers.
4. Extract the few facts that could actually change the decision.
5. Call out breaking changes, deprecations, version differences, and recent shifts when relevant.
6. Separate verified facts from inference.
7. Return a short, decision-relevant brief.

## Rules
- Keep the research bounded to the active question.
- Include concrete dates or versions when freshness matters.
- Do not make the final architecture or implementation decision.
- Do not speculate beyond the evidence.
- Say plainly when something cannot be verified.
- Escalate through the coordinator when sources are contradictory or too thin to support a useful brief.
