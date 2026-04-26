---
id: validation-workflow
cat: conventions
rev: 1
created: 2026-04-26T12:00:00Z
updated: 2026-04-26T12:00:00Z
by: coordinator
tags: [validation, testing, workflow, dotnet, fast-poc]
summary: "Validation baseline, test command, and workflow preferences for shooter."
status: active
---

## Preferences
- Fast PoC loop preferred.
- Skip acceptance-test automation and DoD phases until user says otherwise.

## Validation baseline
- 57/57 tests passing at last handoff.
- Command: `dotnet test shooter.slnx -nologo -v:minimal`

## Rules
- Do NOT run `dotnet build` and `dotnet test` in parallel — triggers transient file-lock issues.
- Expect pre-existing XML doc warnings; ignore unless relevant to active task.
- MVVM Toolkit generates `OpenFileCommand`/`SaveFileCommand`, NOT `OpenFileAsyncCommand`/`SaveFileAsyncCommand`.

## open
- None.
