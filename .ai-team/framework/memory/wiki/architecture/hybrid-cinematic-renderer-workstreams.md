---
id: hybrid-cinematic-renderer-workstreams
cat: architecture
rev: 1
created: 2026-05-06T22:50:00Z
updated: 2026-05-06T22:50:00Z
by: architect
tags: [renderer, opengl, gl33, deferred, ssr, volumetrics, workstreams, sub-agents, shooter]
summary: "Kickoff packet for parallel sub-agent implementation of the GL 3.3 hybrid cinematic renderer roadmap."
refs: [architecture/hybrid-cinematic-renderer-plan]
status: active
---

# Hybrid Cinematic Renderer — Sub-Agent Kickoff Packet

## Shared contract freeze (must be accepted before coding)

### Frozen renderer contracts
- Opaque world path migrates to deferred first.
- Water, viewmodel, particles, tracers, muzzle flashes, decals, and optional SDF VFX stay forward/specialized during the first implementation wave.
- Base frame graph target:
  1. ShadowMap
  2. GBuffer (opaque world)
  3. GBuffer Resolve
  4. SSAO
  5. Deferred Lighting
  6. SSR
  7. Volumetrics
  8. Forward Specials
  9. Bloom
  10. Tonemap / Grade
- GBuffer layout target:
  - G0 = RGBA8(albedo.rgb, ao)
  - G1 = RG16F(octa normal)
  - G2 = RGBA8(roughness, metallic, emissive, wetness)
  - depth = DEPTH_COMPONENT24
- Preserve MSAA in the geometry/GBuffer stage, then resolve before fullscreen passes.
- Selective temporal smoothing only; no default whole-frame TAA.

### Shared ownership boundaries
- WS1 owns frame graph and deferred foundation.
- WS2 owns environment/reflection probe upgrades.
- WS3 owns SSR only.
- WS4 owns shadow filtering/contact shadows.
- WS5 owns volumetrics and volumetric temporal history.
- WS6 owns wetness/material plumbing and validation content.

---

## WS1 — Deferred Foundation
**Mission:** build the new opaque-world deferred core without regressing current post-processing infrastructure.

### Scope
- Add `GBufferTarget` with MSAA + resolve behavior.
- Add `WorldGBufferRenderer` for opaque world brushes.
- Add `DeferredLightingPass` consuming GBuffer + SSAO + shadow + IBL.
- Refactor `OpenGLFrameRenderer` toward the new opaque deferred pass order.

### Primary files
- `src/Shooter.App/Render/GBufferTarget.cs`
- `src/Shooter.App/Render/WorldGBufferRenderer.cs`
- `src/Shooter.App/Render/DeferredLightingPass.cs`
- `src/Shooter.App/Render/Shaders.*`
- `src/Shooter.App/RenderSystem/OpenGLFrameRenderer.cs`
- `src/Shooter.App/RenderSystem/OpenGLPostProcessResources.cs`

### Constraints
- Preserve existing HDR/post path until deferred opaque output reaches parity.
- Do not migrate water or viewmodel into deferred in this workstream.
- Keep build green at every step.

### Validation
- `dotnet build shooter.slnx -nologo`
- world still renders with current fallback path until deferred path is fully wired

---

## WS2 — Reflection Infrastructure
**Mission:** upgrade environment lighting to support glossy/specular fallback for SSR and wet surfaces.

### Scope
- Upgrade `IblProbe` with a prefiltered specular cubemap.
- Define roughness-to-mip sampling convention.
- Provide shader helper contract for specular environment lookup.

### Primary files
- `src/Shooter.App/Render/IblProbe.cs`
- `src/Shooter.App/Render/Shaders.Sky.cs`
- `src/Shooter.App/Render/Shaders.cs`

### Dependencies
- none for initial probe work
- must align with WS1 deferred lighting contract before merge

### Validation
- `dotnet build shooter.slnx -nologo`
- no regressions in sky / irradiance probe generation

---

## WS3 — SSR
**Mission:** add half-resolution screen-space reflections with graceful fallback.

### Scope
- Add `SsrPass`.
- Consume lit HDR, depth, normal, roughness/wetness.
- Add confidence-based fallback to specular environment.
- Add SSR-only temporal history only after base SSR is stable.

### Primary files
- `src/Shooter.App/Render/SsrPass.cs`
- `src/Shooter.App/Render/Shaders.*`
- `src/Shooter.App/RenderSystem/OpenGLPostProcessResources.cs`

### Dependencies
- WS1
- WS2

### Validation
- `dotnet build shooter.slnx -nologo`
- no black reflection holes; misses fade to cubemap fallback

---

## WS4 — Shadow Quality
**Mission:** improve grounded shadow richness without major cost explosions.

### Scope
- Improve shadow-map filtering (better PCF / PCSS-lite).
- Add short-range contact shadows.

### Primary files
- `src/Shooter.App/Render/ShadowMap.cs`
- `src/Shooter.App/Render/ContactShadowPass.cs`
- `src/Shooter.App/Render/Shaders.*`

### Dependencies
- none initially
- final integration with WS1 deferred lighting required

### Validation
- `dotnet build shooter.slnx -nologo`
- stable shadow quality without heavy flicker/popping

---

## WS5 — Volumetrics
**Mission:** add cinematic atmospheric depth at practical cost.

### Scope
- Quarter-resolution volumetric fog raymarch.
- Bilateral/depth-aware upscale.
- Optional volumetric-only temporal history.

### Primary files
- `src/Shooter.App/Render/VolumetricFogPass.cs`
- `src/Shooter.App/Render/Shaders.*`
- `src/Shooter.App/RenderSystem/OpenGLPostProcessResources.cs`

### Dependencies
- WS1

### Validation
- `dotnet build shooter.slnx -nologo`
- cost remains bounded at quarter resolution

---

## WS6 — Wetness / Materials / Content
**Mission:** add authored wet response and validation content.

### Scope
- Add wetness scalar through material/runtime data paths.
- Apply wetness in deferred lighting and SSR weighting.
- Add validation surfaces/maps/content.

### Primary files
- `src/MapEditor.Core/Entities/*`
- `src/MapEditor.Formats/*`
- `src/Shooter.App/Game/WorldBrushFactory.cs`
- `src/Shooter.App/Render/*`
- `dust.shmap`

### Dependencies
- WS1
- WS2

### Validation
- `dotnet build shooter.slnx -nologo`
- authored wet materials visibly change roughness/reflection response

---

## Merge order
1. WS1
2. WS2
3. WS4
4. WS3 + WS6
5. WS5
6. final integration and stability pass

---

## Global integration checklist
- texture-unit assignments do not collide
- resolve order is correct between GBuffer, HDR, SSR, and volumetrics
- deferred opaque and forward special passes do not double-light
- selective temporal history rejects invalid reprojection cleanly
- MSAA resolve points are explicit and documented
