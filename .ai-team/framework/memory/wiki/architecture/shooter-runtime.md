---
id: shooter-runtime
cat: architecture
rev: 4
created: 2026-04-29T16:00:00Z
updated: 2026-05-06T22:30:00Z
by: developer
tags: [shooter, runtime, opengl, metal, silk-net, gameplay, weapons, rendering, textures, backends, deferred, msaa]
summary: "Shooter.App game runtime: physics, weapons, rendering, asset pipeline."
refs: [architecture/module-boundaries, decisions/glb-asset-pipeline]
status: active
---

## Pipeline (current OpenGL runtime)
See `architecture/lighting-pipeline` for the detailed pass chain. The active production renderer is now an OpenGL **hybrid deferred opaque + forward specials** pipeline.

Current strategy:
- render standard opaque world brushes into a multisampled GBuffer
- resolve and light that GBuffer in a deferred fullscreen pass
- keep non-standard opaque, water, pickups, viewmodel, rockets, decals, particles, and other special cases on forward/specialized paths
- resolve the multisampled HDR target before post work such as SSR, bloom, auto-exposure, and tonemap

Important current facts:
- **4x MSAA is still active** on both the GBuffer and the HDR scene target
- the renderer resolves multisampled attachments into standard 2D textures before most post-processing
- **volumetric fog was removed** after visual evaluation because it dulled scene color/contrast too much
- world-surface **relief** is again threaded through the deferred standard-opaque path, so both the relief toggle and relief-strength slider affect the actual main world render
- default relief strength is now `0.090`
- recent shadow tuning increased bias / polygon offset and tightened contact-shadow defaults to suppress diagonal self-shadow patterns on walls and floors

The runtime supports two material-upgrade paths: (1) Shooter-side companion maps discovered by filename convention (`_normal`, `_roughness`, `_metallic`, `_ao`, `_height`), and (2) authored brush-level material properties persisted through `.shmap`. The authored material model currently supports Standard, Water, and Lava behavior families.

Metal/backend-abstraction code from earlier work still exists in the repository, but the currently iterated visual reference path is the OpenGL renderer above.

## Module
- `Shooter.App` — single-exe game consuming `.shmap` 1.4.0 files via `MapEditor.Formats`.
- Net10.0, Silk.NET windowing, retina via `FramebufferSize`.
- Current production visual path is OpenGL with hybrid deferred + forward rendering.

## Subsystems
- `Game/`: `GameWorld`, `Player`, `WeaponSystem`, `PickupSystem`, `BulletHoleManager`, `ScorchManager`, `RocketSystem`, `MuzzleFlash`, `TracerSystem`, particles, runtime menu/light settings.
- `Physics/`: `CollisionWorld` — sphere-vs-triangle, Möller-Trumbore raycast, AABB cull.
- `Render/`: OpenGL renderer stack (`WorldRenderer`, `WorldGBufferRenderer`, `DeferredLightingPass`, `ContactShadowPass`, `SsrPass`, `DecalRenderer`, `ScorchRenderer`, `TracerRenderer`, `RocketRenderer`, `WeaponViewmodelRenderer`, `MuzzleFlashRenderer`, `HudRenderer`, shared `TexturedModelRenderer`, `GpuModel`, `ModelData`, `Shaders`, `TextureCache`).
- `RenderSystem/`: frame orchestration / backend ownership (`OpenGLFrameRenderer`, `OpenGLRenderBackend`, resource containers, and older backend-abstraction leftovers from the Metal work).
- `Platform/Metal/`: earlier Objective-C / QuartzCore / Metal interop helpers retained in-repo.

## Player + physics
- Sphere collider (`Radius=0.4`); `HalfHeight=0.9` is eye/camera geometry only.
- Spawn snap-to-floor via downward raycast; sphere center sits `Radius + 0.02` above the floor.
- `OnUpdate` clamps `dt` to `1/30s`; `Velocity.Y` clamped to terminal `-24 m/s` to prevent tunneling.

## Weapons
- 3 kinds: AK-47 (auto, hitscan), Shotgun (8 pellets, hitscan), RocketLauncher (projectile, splash).
- `WeaponDef` carries `FireMode`, `ProjectileSpeed`, `SplashRadius`, `RecoilStrength`.
- Hitscan weapons emit a view-space muzzle flash. Rocket spawns a `Rocket` entity.

## View-space anchoring
- `WeaponViewmodelRenderer.MuzzleViewOffset = (0.20, -0.16, -0.55)`, `FovYRadians = 75°`.
- Rocket muzzle world-pos derives from this offset using camera basis so projectiles leave the visible barrel.
- Per-weapon flash anchor + `RecoilStrength` tuned at the trigger site in `Program.cs` / runtime setup.

## World-brush texturing + materials
- Static world brushes can bind a diffuse/base-color texture when `material_name` is an image path.
- Resolution order: absolute path → `AssetLocator.Root`-relative path → cwd-relative path.
- Missing files degrade to hashed tint instead of failing map load.
- `TextureCache` uploads and caches base color plus companion maps once per resolved texture path.
- Brush material behavior can be authored directly in `.shmap` through brush-level `material_properties`.

## Scorch + flash
- Rocket detonation → one `Scorch` decal sized at ~55% of splash radius.
- Muzzle flash: additive cross-quad billboard, view-space, ~55 ms, randomized rotation/scale per shot.
