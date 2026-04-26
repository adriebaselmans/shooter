---
id: coordinate-contract
cat: architecture
rev: 1
created: 2026-04-26T12:00:00Z
updated: 2026-04-26T12:00:00Z
by: coordinator
tags: [coordinates, axes, left-handed, y-up, viewports, colors]
summary: "Left-handed Y-up coordinate contract and viewport axis mappings."
refs: [incidents/known-gotchas]
status: active
---

## System
- Left-handed, Y-up.
- `+X = right`, `+Y = up`, `+Z = forward`.

## Viewport projections
| View  | Horizontal | Vertical |
|-------|-----------|---------|
| Top   | X right   | Z up    |
| Front | X right   | Y up    |
| Side  | Z right   | Y up    |

## Axis colors (strict — do not change)
- X = red
- Y = green
- Z = blue
- 3D floor guide must match Top(XZ): X=red, Z=blue.

## open
- None.
