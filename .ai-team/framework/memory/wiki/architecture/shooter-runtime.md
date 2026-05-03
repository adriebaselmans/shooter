---
id: shooter-runtime
cat: architecture
rev: 3
created: 2026-04-29T16:00:00Z
updated: 2026-05-01T13:05:00Z
by: coordinator
tags: [shooter, runtime, opengl, metal, silk-net, gameplay, weapons, rendering, textures, backends]
summary: "Shooter.App game runtime: physics, weapons, rendering, asset pipeline."
refs: [architecture/module-boundaries, decisions/glb-asset-pipeline]
status: active
---

## Pipeline (post-Lighting-Pass)
See `architecture/lighting-pipeline` for the full HDR pass chain. The production OpenGL backend still draws world geometry into an offscreen HDR target with shadows + IBL ambient + Lambert direct, then bloom + ACES tone map composite to the default framebuffer; HUD on top.

The Metal backend now renders the static textured brush world, held weapon model, active rocket models, decals/scorches/muzzle flash overlays, SSAO, bloom, and HUD through a fuller multi-pass HDR pipeline. It uses a directional shadow map, HDR scene color + normal MRT, sampleable depth, post-combined SSAO/bloom tone mapping, and a final HUD pass over the drawable.

Current Metal strategy: attach `CAMetalLayer` to the Silk Cocoa window, batch world/model/effect geometry into one explicit dynamic vertex layout, render a stable player-centered directional shadow map, shade the main scene in GPU fragment code using sun + hemispheric ambient + shadow visibility, write HDR color plus view-space normals, run fullscreen SSAO and bloom passes, tone-map to the drawable, then draw HUD quads last. The visual quality pass further adds tuned exposure/bloom defaults, roughness/specular/detail-normal material parameters, softer shadow filtering, distance/height fog, and post color grading so the backend is no longer just feature-complete but also visually better balanced.

Across both backends, the renderer now trends more toward explicit GPU-side material/light/post data than toward deeper CPU-baked final shading. On Metal specifically, a first hybrid path-tracing layer now exists: primary visibility stays rasterized, while a reduced-resolution progressive GI pass reconstructs first-hit raster data, traces secondary visibility against static world triangles, accumulates history while the camera is stable, and blends the result in final post.

This is not a full path-traced renderer yet, but it is the repository's first real raster/path-tracing coexistence point.

The runtime also now supports two material-upgrade paths: (1) a purely Shooter-side fallback where companion normal/roughness/AO maps are discovered by filename convention, and (2) an authored brush-level material-properties model coming from the map editor and `.shmap`. The authored model currently supports Standard, Water, and Lava behavior families with parameters for roughness, specular, normal strength, emissive, opacity, flow, distortion, fresnel, and pulse.

## Module
- `Shooter.App` — single-exe game consuming `.shmap` 1.3.0 files via `MapEditor.Formats`.
- Net10.0, Silk.NET windowing, retina via `FramebufferSize`.
- Rendering is now backend-selected at startup: default `OpenGL`, optional macOS `Metal` bootstrap.

## Subsystems
- `Game/`: `GameWorld`, `Player`, `WeaponSystem`, `PickupSystem`, `BulletHoleManager`, `ScorchManager`, `RocketSystem`, `MuzzleFlash`, `TracerSystem` (idle).
- `Physics/`: `CollisionWorld` — sphere-vs-triangle, Möller-Trumbore raycast, AABB cull.
- `Render/`: existing OpenGL feature renderer stack (`WorldRenderer`, `DecalRenderer`, `ScorchRenderer`, `TracerRenderer`, `RocketRenderer`, `WeaponViewmodelRenderer`, `MuzzleFlashRenderer`, `HudRenderer`, shared `TexturedModelRenderer`, `GpuModel`, `ModelData`, `Shaders`, `TextureCache`).
- `RenderSystem/`: backend abstraction layer (`IRenderBackend`, `RenderBackendFactory`, `OpenGLRenderBackend`, `MetalBootstrapBackend`).
- `Platform/Metal/`: tiny Objective-C / QuartzCore / Metal interop helpers for the bootstrap backend.

## Player + physics
- Sphere collider (`Radius=0.4`); `HalfHeight=0.9` is *eye geometry only*, never used in collision math.
- Spawn: snap-to-floor via downward raycast; sphere center sits `Radius+0.02` above the floor.
- `OnUpdate` clamps `dt` to `1/30s`; `Velocity.Y` clamped to terminal `-24 m/s` to prevent tunneling.

## Weapons
- 3 kinds: AK-47 (auto, hitscan), Shotgun (8 pellets, hitscan), RocketLauncher (projectile, splash).
- `WeaponDef` carries `FireMode`, `ProjectileSpeed`, `SplashRadius`, `RecoilStrength`.
- Hitscan weapons emit a view-space muzzle flash (no tracers). Rocket spawns a `Rocket` entity.

## View-space anchoring
- `WeaponViewmodelRenderer.MuzzleViewOffset = (0.20, -0.16, -0.55)`, `FovYRadians = 75°`.
- Rocket muzzle world-pos derives from this offset using camera basis so projectiles leave the visible barrel.
- Per-weapon flash anchor + `RecoilStrength` tuned at the trigger site in `Program.cs` switch.

## World-brush texturing
- Static world brushes can now bind a diffuse/base-color texture when `material_name` is an image path (`.png`, `.jpg`, `.jpeg`, `.bmp`).
- Resolution order: absolute path → `AssetLocator.Root`-relative path → cwd-relative path.
- `GameWorld.FromScene` resolves `WorldBrush.TexturePath`; missing files degrade to the old hashed tint path instead of failing map load.
- `WorldRenderer` owns a small `TextureCache` keyed by absolute file path; textures are uploaded once, sampled through the existing brush UVs from `MeshGenerator`, and lit by the same HDR/IBL/shadow pipeline as before.
- Current scope is **one texture per brush**, not per-face `surface_mappings` in Shooter.App.

## Scorch + flash
- Rocket detonation → one `Scorch` decal sized at ~55% of splash radius. Noise-perturbed radial smudge shader.
- Muzzle flash: additive cross-quad billboard, view-space, ~55 ms, randomized rotation/scale per shot.
