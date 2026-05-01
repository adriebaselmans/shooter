---
id: shooter-runtime
cat: architecture
rev: 2
created: 2026-04-29T16:00:00Z
updated: 2026-04-29T19:55:00Z
by: coordinator
tags: [shooter, runtime, opengl, silk-net, gameplay, weapons, rendering, textures]
summary: "Shooter.App game runtime: physics, weapons, rendering, asset pipeline."
refs: [architecture/module-boundaries, decisions/glb-asset-pipeline]
status: active
---

## Pipeline (post-Lighting-Pass)
See `architecture/lighting-pipeline` for the full HDR pass chain. World draws into an offscreen HDR target with shadows + IBL ambient + Lambert direct, then bloom + ACES tone map composite to the default framebuffer; HUD on top.

## Module
- `Shooter.App` — single-exe game consuming `.shmap` 1.3.0 files via `MapEditor.Formats`.
- Net10.0, Silk.NET windowing/OpenGL, retina via `FramebufferSize`.

## Subsystems
- `Game/`: `GameWorld`, `Player`, `WeaponSystem`, `PickupSystem`, `BulletHoleManager`, `ScorchManager`, `RocketSystem`, `MuzzleFlash`, `TracerSystem` (idle).
- `Physics/`: `CollisionWorld` — sphere-vs-triangle, Möller-Trumbore raycast, AABB cull.
- `Render/`: `WorldRenderer`, `DecalRenderer`, `ScorchRenderer`, `TracerRenderer`, `RocketRenderer`, `WeaponViewmodelRenderer`, `MuzzleFlashRenderer`, `HudRenderer`, plus shared `TexturedModelRenderer`, `GpuModel`, `ModelData`, `Shaders`, `TextureCache`.

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
