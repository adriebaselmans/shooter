---
id: known-gotchas
cat: incidents
rev: 1
created: 2026-04-26T12:00:00Z
updated: 2026-04-26T12:00:00Z
by: coordinator
tags: [gotchas, wpf, hwndhost, keyboard, axis-colors, shader, mvvm-toolkit]
summary: "Known recurring pitfalls in the shooter map editor codebase."
refs: [architecture/coordinate-contract, conventions/validation-workflow]
status: active
---

## Gotchas

### WPF + HwndHost keyboard routing
- Keyboard shortcuts do NOT auto-route when GL child HWND owns focus.
- Explicit shortcut router is required. Do not remove it.

### Axis color misdiagnosis
- Side/Top viewport bugs easy to misdiagnose as math when the real issue is axis color mismatch.
- Always verify color assignment before diving into math.

### Perspective floor guide colors
- Must match Top(XZ) convention: X=red, Z=blue. Non-negotiable.

### Perspective shader visibility
- Do not rely purely on scene lighting for editor brush visibility.
- Minimum editor tint must be preserved in the 3D solid shader.

### MVVM Toolkit command naming
- Generates `OpenFileCommand`/`SaveFileCommand`.
- Does NOT generate `OpenFileAsyncCommand`/`SaveFileAsyncCommand`.

### Parallel build/test
- Do not run `dotnet build` and `dotnet test` in parallel — transient file-lock issues.

## open
- None.
