---
id: current-behavior
cat: context
rev: 1
created: 2026-04-26T12:00:00Z
updated: 2026-04-26T12:00:00Z
by: coordinator
tags: [behavior, ux, editor, viewports, shortcuts, primitives]
summary: "Stable user-facing behaviors that must not regress."
status: active
---

## Must stay true
- Window behaves like a normal desktop app (movable, maximize, close).
- Orthographic views show logical projections matching 3D world axes.
- 3D view shows readable translucent brush colors matching orthographic color language.
- Selected brushes movable by direct manipulation in orthographic editing.
- Brush creation visible in realtime in orthographic and 3D views.
- Open/save/menu/toolbar/shortcut flows work.
- Ctrl-based shortcuts work even when a GL viewport has focus.
- Primitive creation uses toolbar buttons (Box, Cylinder, Cone, Wedge), not a dropdown.
- Primitive buttons are mutually exclusive single-use toggles: activate → create mode; click active again → select mode; completing brush creation → auto-return to select mode.

## Best next steps
- Runtime verification/polish of viewport interaction feel.
- UX polish: axis labels, clearer manipulators, better toolbar spacing/icons.

## open
- None.
