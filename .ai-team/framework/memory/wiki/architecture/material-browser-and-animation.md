---
id: material-browser-and-animation
cat: architecture
rev: 3
created: 2026-04-26T20:00:00Z
updated: 2026-04-26T20:45:00Z
by: developer
tags: [avalonia, materials, textures, animation, water, lava]
summary: "Avalonia material browser and shader-driven animated texture metadata."
refs: [architecture/viewport-hosting, context/current-behavior]
status: active
---

## model
- `TextureAssetDescriptor`: key, file, color, kind, animation.
- `TextureLibraryEntry`: UI mirror with category, kind label, animation label, usage hint.
- Built-ins: `liquids/water_animated`, `hazards/lava_animated`.

## ui
- Browser: collections, search, kind filter, animated-only toggle.
- Inspector: preview, key, kind, color, motion, usage, apply actions.
- Right panel keeps selected material context near surface mapping.

## render
- `TextureGpuCache` creates procedural water/lava textures when no file exists.
- Perspective shader uses time, kind, flow, pulse uniforms.
- Water scrolls/waves UVs; lava scrolls/pulses emissive color.

## open
- Add imported frame/spritesheet authoring when real animated art arrives.