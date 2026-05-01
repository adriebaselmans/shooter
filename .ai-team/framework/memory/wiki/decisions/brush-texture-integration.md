---
id: brush-texture-integration
cat: decisions
rev: 1
created: 2026-04-29T19:55:00Z
updated: 2026-04-29T19:55:00Z
by: architect
tags: [textures, shooter, brushes, cc0, polyhaven, rendering, assets, decisions]
summary: "Shooter.App world brushes use image-backed material_name paths for diffuse-only texture sampling with tint fallback."
refs: [architecture/shooter-runtime, conventions/validation-workflow]
status: active
---

## Decision
Use **image-backed `material_name` strings** in `.shmap` for Shooter.App brush texturing.
If `material_name` ends with `.png`, `.jpg`, `.jpeg`, or `.bmp`, the shooter runtime treats it as an asset-relative or absolute image path, loads it once through a small GL texture cache, and samples it in `WorldFrag` using the existing mesh UVs.

## Chosen source model
- Imported texture content is a tiny curated set of **Poly Haven CC0** diffuse maps.
- Files live in `assets/textures/dust/`.
- The `.shmap` references those files directly via paths like `textures/dust/ground_sand_1k.jpg`.

## Why this path
- Keeps the map self-describing — no extra manifest layer.
- Preserves backward compatibility with old maps using plain material keys like `brick` or `sand`.
- Reuses existing UV generation from `MapEditor.Core.Geometry.MeshGenerator`.
- Avoids a new dependency on `MapEditor.Rendering` internals.

## Explicit non-goals
- No normal/roughness/PBR workflow for brush geometry yet.
- No runtime online download/import.
- No per-face surface mapping support in Shooter.App yet; current path is one texture per brush.

## Fallback behavior
If the file is missing or unreadable, the brush renders with the existing hashed tint color. The map still loads and gameplay continues.
