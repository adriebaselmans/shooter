---
id: implementation-history
cat: context
rev: 8
created: 2026-04-26T12:00:00Z
updated: 2026-05-06T22:30:00Z
by: developer
tags: [history, implemented, editor, viewports, brushes, gl, metal, wpf, avalonia, shooter, weapons, glb, lighting, hdr, shadows, textures, backends, deferred, msaa]
summary: "Completed implementation milestones for the shooter map editor and game runtime."
status: active
---

## Completed milestones
- Initial editor shell: 3 orthographic viewports + 1 3D viewport.
- WI-10 interaction loop: tool contracts, selection state, viewport input routing, select/create/move behavior, UI sync, hardening.
- Fixed GL viewport host: was using `STATIC` child window — mouse input never reached tool path.
- Visible orthographic grids + 3D floor/orientation guide.
- Realtime brush creation/movement in orthographic and perspective rendering.
- Removed dedicated resize-mode workflow; orthographic resize from selection handles directly.
- Fixed 3D mouse wheel crash (unsafe 64-bit wheel message decoding).
- Per-session logging + global managed exception logging.
- Restored normal desktop shell behavior (movable window, maximize/close, menu/toolbar/file flows).
- Fixed open/save/shortcut behavior (MVVM Toolkit command naming gotcha).
- Keyboard routing from native GL child window back into WPF command handling.
- Editor-local copy/paste, undo/redo, viewport maximize/restore, properties editing.
- Replaced primitive dropdown with explicit buttons: Box, Cylinder, Cone, Wedge.
- Primitive buttons as mutually exclusive single-use toggles.
- Unified brush colors via shared `BrushColorPalette`.
- Fixed perspective brushes appearing black/transparent in low light (minimum editor tint in 3D solid shader).
- Realigned all viewport interaction/rendering/camera/grid/resize math to axis contract.
- Fixed Top(XZ) vs 3D mismatch by correcting perspective floor-guide X/Z colors.
- Added UI-neutral input contracts: `ViewportPoint`, `ViewportVector`, `EditorKey`, `EditorModifierKeys`.
- Moved file/message dialogs behind shared service interfaces for non-WPF shells.
- Added `MapEditor.App.Avalonia` as the active desktop shell.
- Ported main shell layout, four-viewport composition, and shortcut routing to Avalonia.
- Replaced Avalonia Windows-native host approach with cross-platform `OpenGlControlBase` host.
- Active solution now builds around Avalonia app + core + formats + rendering on macOS.

## Shooter.App game/runtime milestones
- Shooter baseline: movement, sphere collision, raycast, hitscan + projectile weapons, decals, HUD, fallback arena.
- GLB asset pipeline (SharpGLTF) replacing FBX/Assimp.
- Textured world brushes, runtime companion maps, authored brush material properties, water/lava material families.
- OpenGL lighting/HDR foundation: analytic sky, IBL, shadow map, bloom, ACES, auto-exposure.
- Renderer refactor: `GameSession`, split shader files, `OpenGLFrameRenderer`, render/resource containers, world/material helper classes.
- Relief feature evolution: companion height maps → heuristic height fallback → POM experiments removed due to seams → subtle relief/bump shading.
- Anti-aliasing evolution: TAA and FXAA removed; hardware **4x MSAA** adopted and preserved.
- Water evolution: authored water materials → normal-map water → dedicated refraction/reflection pass → procedural displaced water surface.
- Hybrid renderer evolution: deferred opaque foundation, contact shadows, SSR, and specialized forward passes layered into the OpenGL path.

## Latest rendering follow-up (2026-05-06)
- Volumetric fog feature and menu toggle were removed after visual review because they dulled the image.
- Relief was restored in the deferred standard-opaque path after a regression where the menu toggle/slider no longer affected the main world render.
- Relief strength was changed to drive visible normal/blend/shadow contribution directly, not just a weak internal slope term.
- Default relief strength was raised to `0.090`.
- Diagonal wall/floor self-shadow patterns were reduced by tuning shadow bias, polygon offset, and contact-shadow defaults.
- MSAA state was re-verified: current OpenGL path still uses 4x multisampled GBuffer + 4x multisampled HDR target, both resolved before post.

## open
- None.
