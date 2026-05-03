---
id: implementation-history
cat: context
rev: 7
created: 2026-04-26T12:00:00Z
updated: 2026-05-01T13:05:00Z
by: coordinator
tags: [history, implemented, editor, viewports, brushes, gl, metal, wpf, avalonia, shooter, weapons, glb, lighting, hdr, shadows, textures, backends]
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

## open
- None.

## Shooter.App game runtime (2026-04-29)
- Coordinator-flow delivery: requirements / design / review / DoD YAMLs filled out.
- Map format bumped 1.2.0 → 1.3.0; spawn points + pickups authored in editor.
- Editor pickup menu: AmmoAk47 / AmmoShotgun / AmmoRocket / WeaponShotgun / WeaponRocketLauncher.
- Shooter.App baseline: WASD + mouse, sphere collision, Möller-Trumbore raycast, hit-scan + projectile weapons, decals, HUD, fallback arena.
- 3 weapons: AK-47 (auto, hitscan), Shotgun (8 pellets, hitscan), Rocket Launcher (projectile, splash). All 3 owned at start.
- Per-weapon `RecoilStrength` driving viewmodel kick.
- View-space muzzle flash (additive billboard) with per-weapon anchor + scale; tracers retired for hitscan.
- Rocket projectile system: integration, segment-raycast, detonation → single noisy scorch decal sized to splash.
- GLB asset pipeline (SharpGLTF) replacing FBX/Assimp; `ModelData.AlignBarrelToForward` anchors each model's barrel tip at origin.
- Shared `MuzzleViewOffset` + `FovYRadians` between viewmodel renderer and projectile spawn so visuals stay aligned through recoil.
- Procedural fallback arena: 60×60 floor, perimeter walls, central platform with two pitched ramps, two side platforms, columns, crates, alley, scattered pickups.
- Spawn snap-to-floor via downward raycast; uses `Player.Radius` (sphere collider) not `HalfHeight`.
- `OnUpdate` clamps `dt` to `1/30s`; `Player.Update` clamps fall to `-24 m/s` terminal velocity (anti-tunnel).

## Shooter.App lighting pass (2026-04-29)
- Coordinator-flow delivery: requirements / design / review / DoD YAMLs filled out.
- HDR-linear forward pipeline: RGBA16F offscreen target replaces direct default-framebuffer rendering.
- Procedural analytic sky (blue zenith + warm horizon + Mie sun glow + ground); drawn as unit cube with depth=1 trick.
- Image-based ambient: 64² sky cubemap + 16² irradiance cubemap (one-shot, 64 hemisphere samples/texel).
- 2048² single-cascade directional shadow map with hardware PCF (`sampler2DShadow`), 3×3 kernel, slope-scaled bias, front-face cull during shadow pass.
- Lambert direct lighting in `WorldFrag` and `TexturedModelFrag` via shared `LightingHeader` snippet; Phong removed.
- Muzzle flash multiplied by HDR_BOOST=6.0 to survive ACES tone curve.
- Bloom: threshold @ 0.9 with knee → 5-mip Karis-13 downsample → 9-tap tent-filter additive upsample.
- Final post pass: ACES filmic (Narkowicz) + gamma 1/2.2, Exposure uniform.
- HUD draws after PostFx into default framebuffer; unaffected by exposure/bloom.
- New: `Game/LightingEnvironment.cs`, `Render/{HdrTarget,SkyRenderer,IblProbe,ShadowMap,Bloom,PostFx}.cs`. Modified: `Shaders.cs`, `WorldRenderer`, `TexturedModelRenderer`, `WeaponViewmodelRenderer`, `RocketRenderer`, `Program.cs`.
- Single `LightingEnvironment` instance threaded through every renderer as the source of truth (sun direction, sun color/intensity, turbidity, ground albedo, exposure, irradiance intensity).

## Shooter.App dust texture pass (2026-04-29)
- Dust-style playable map authored as `dust.shmap`: open courtyard, covered tunnel, enclosed interior room, crates, platform, pickups, and multiple spawns.
- Imported five Poly Haven CC0 1k diffuse maps into `assets/textures/dust/` and documented source/license in `assets/textures/dust/LICENSES.md`.
- Shooter.App world-brush path upgraded from tint-only to optional diffuse texture sampling using the existing UVs authored by `MeshGenerator`.
- New `Render/TextureCache.cs` caches GL uploads by file path and owns white-fallback texture disposal.
- `GameWorld.FromScene` now resolves image-backed `material_name` values into absolute `TexturePath` values; missing files remain non-fatal and fall back to hashed tint.
- `dust.shmap` now applies distinct sand / plaster / stone / concrete / wood materials by brush category.
- Follow-up Dust polish pass: map rebuilt to 59 brushes / 708 tris with explicit A-long, mid, and B-tunnel/B-market route identities.
- Added surface-mapping UV tuning directly in `dust.shmap` for large floors, walls, ceilings, cover, crates, trims, and awnings.
- Replaced the too-subtle outer-wall plaster with a stronger brick texture and sank tall walls slightly into the ground to eliminate the visible floating-gap impression.
- Imported extra Poly Haven CC0 textures for outer walls, awnings, and trim (`brick_wall_001`, `blue_painted_planks`, `brown_planks_05`).
- Grounding hotfix after visual feedback first tried map-side overlap/plinth adjustments, but those were ultimately superseded by the real engine fix below.
- Real root cause fixed in engine: `MeshGenerator.GenerateBox` top and bottom face winding corrected, which resolved the persistent floor/wall seam artifact across box-based geometry.
- Final cleanup pass removed the temporary seam-debug runtime toggles, normalized floor-contact map authoring, kept the genuine structural support additions (awning posts / lintel connections), and rebalanced Dust map pickups/facade accents.

## Shooter.App renderer abstraction + Metal bootstrap (2026-05-01)
- Program-level rendering ownership moved behind `RenderSystem/IRenderBackend`.
- Existing feature-complete OpenGL path wrapped as `OpenGLRenderBackend` and remains the default backend.
- Backend selection added via CLI (`--backend=gl|metal`).
- Concrete macOS `MetalBootstrapBackend` implemented using Silk's Cocoa window handle plus Objective-C / QuartzCore / Metal interop.
- Metal bootstrap backend attaches a `CAMetalLayer`, creates `MTLDevice` + command queue, acquires a drawable, encodes a render-pass clear, and presents.
- Scope deliberately stops at clear/present bootstrap; feature parity with the OpenGL gameplay renderer remains future work.
- Metal phase 2: static brush world now renders on Metal using uploaded triangle soup, a real depth attachment, and CPU-side ambient + Lambert flat lighting from the live player camera.
- Metal phase 3: textured world rendering added on Metal via per-brush batches, preserved MeshGenerator UVs, per-brush Metal textures loaded from TexturePath, and white-texture fallback for missing assets.
- Metal phase 4: held-weapon viewmodel, active rocket models, and primitive HUD rendering added on top of the textured Metal world path.
- Metal phase 5: HDR offscreen target + ACES-style tone-mapping post pass added; HUD remains a final pass over the drawable.
- Metal phase 6: directional shadow map, HDR scene MRT normal output, SSAO, bloom, decals/scorches/muzzle flash overlays, and richer GPU-side scene lighting added to the Metal backend.
- Visual quality pass: tuned exposure/bloom/light defaults, added heuristic roughness/specular/detail-normal material response, softened shadow filtering, introduced fog/haze and post color grading, and mirrored the direction across OpenGL and Metal.
- Hybrid path tracing phase 1: Metal gained a low-resolution progressive indirect-GI pass that traces against static world triangles and blends into raster post output.
- Visual runtime big pass (no editor/schema changes): companion normal/roughness/AO maps by filename convention, higher-quality shadows, denoised hybrid GI, and lightweight smoke/dust/debris particle polish added.
- Authored material-properties phase: map/editor/runtime now share a brush-level material model supporting Standard/Water/Lava with persisted .shmap data and Dust includes a water demo brush.
