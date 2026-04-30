---
id: shooter-runtime
cat: architecture
rev: 1
created: 2026-04-29T16:00:00Z
updated: 2026-04-29T16:00:00Z
by: coordinator
tags: [shooter, runtime, opengl, silk-net, gameplay, weapons, rendering]
summary: "Shooter.App game runtime: physics, weapons, rendering, asset pipeline."
refs: [architecture/module-boundaries, decisions/glb-asset-pipeline]
status: active
---

## Module
- `Shooter.App` — single-exe game consuming `.shmap` 1.3.0 files via `MapEditor.Formats`.
- Net10.0, Silk.NET windowing/OpenGL, retina via `FramebufferSize`.

## Subsystems
- `Game/`: `GameWorld`, `Player`, `WeaponSystem`, `PickupSystem`, `BulletHoleManager`, `ScorchManager`, `RocketSystem`, `MuzzleFlash`, `TracerSystem` (idle).
- `Physics/`: `CollisionWorld` — sphere-vs-triangle, Möller-Trumbore raycast, AABB cull.
- `Render/`: `WorldRenderer`, `DecalRenderer`, `ScorchRenderer`, `TracerRenderer`, `RocketRenderer`, `WeaponViewmodelRenderer`, `MuzzleFlashRenderer`, `HudRenderer`, plus shared `TexturedModelRenderer`, `GpuModel`, `ModelData`, `Shaders`.

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

## Scorch + flash
- Rocket detonation → one `Scorch` decal sized at ~55% of splash radius. Noise-perturbed radial smudge shader.
- Muzzle flash: additive cross-quad billboard, view-space, ~55 ms, randomized rotation/scale per shot.
