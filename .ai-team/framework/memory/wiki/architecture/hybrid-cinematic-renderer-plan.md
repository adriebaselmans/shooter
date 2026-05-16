---
id: hybrid-cinematic-renderer-plan
cat: architecture
rev: 1
created: 2026-05-06T22:35:00Z
updated: 2026-05-06T22:35:00Z
by: architect
tags: [renderer, opengl, gl33, deferred, ssr, volumetrics, pbr, wetness, shadows, shooter, roadmap]
summary: "Repository-specific plan for a GL 3.3 hybrid cinematic renderer: deferred opaque lighting, SSR, contact shadows, volumetrics, wet materials, and selective temporal stabilization."
refs: [architecture/lighting-pipeline, architecture/shooter-runtime, architecture/pbr-upgrade-plan]
status: active
---

# Hybrid Cinematic Renderer Plan (OpenGL 3.3)

## Goal
Upgrade `Shooter.App` toward a modern cinematic "pseudo-RTX" look on legacy OpenGL 3.3 hardware using only:

- rasterization
- fullscreen fragment shaders
- screen-space raymarching
- post-processing

This plan is intentionally **repository-specific**. It assumes the current Shooter renderer already has:

- shadow maps
- HDR target / resolve flow
- SSAO
- bloom / tonemap
- sky + irradiance IBL
- PBR foothold
- forward/specialized water rendering

The proposal is to evolve that footing into a **hybrid deferred opaque + forward specials** pipeline rather than rewriting every draw path at once.

---

## Design principles

1. **Opaque world first, specials later**  
   Move opaque world brushes into deferred lighting first. Keep water, viewmodel, particles, decals, rockets, tracers, muzzle flashes, and optional SDF VFX in forward or specialized passes.

2. **GL 3.3 practical, not purist**  
   Preserve geometry-stage MSAA, resolve into standard textures, then perform fullscreen lighting/post on resolved textures.

3. **Selective temporal only**  
   Apply temporal stabilization to noisy screen-space effects (SSR, volumetric fog, maybe contact shadows). Do not reintroduce whole-frame TAA blur as the baseline AA strategy.

4. **Reflection quality sells the look**  
   SSR + specular cubemap fallback + wet materials are higher priority than chasing fake full GI.

5. **Atmosphere sells scale**  
   Quarter-resolution volumetric fog with shadowed in-scattering is one of the highest-value cinematic features.

---

## Target frame graph

```text
ShadowMap
-> GBuffer (opaque world only, MSAA)
-> GBuffer Resolve
-> SSAO
-> Deferred Lighting (opaque HDR)
-> SSR
-> Volumetrics
-> Forward Specials
   - water
   - decals
   - rockets/models still forward
   - particles / tracers / flashes
   - weapon viewmodel
-> Bloom
-> Tonemap / Grade
```

---

## What stays forward vs deferred

### Deferred first
- opaque world brushes
- later optional opaque world-space models, only after the core path is stable

### Forward / specialized for now
- water
- weapon viewmodel
- particles
- tracers
- muzzle flashes
- decals (unless later migrated)
- rockets / dynamic world models (optional later migration)
- optional SDF raymarched VFX

This split gives the project the benefits of deferred rendering without forcing an all-at-once rewrite.

---

## Proposed GBuffer layout

A compact GL 3.3-friendly layout is enough.

### G0 — Albedo + AO
- Format: `RGBA8`
- `rgb = albedo`
- `a = AO`

### G1 — Normal
- Format: `RG16F`
- octahedral-encoded normal

### G2 — Material params
- Format: `RGBA8`
- `r = roughness`
- `g = metallic`
- `b = emissiveStrength`
- `a = wetness`

### Depth
- Format: `DEPTH_COMPONENT24`
- reconstruct view/world position from depth during fullscreen passes

### Why this layout
- small enough for GL 3.3 bandwidth budgets
- avoids a position target
- encodes the new wetness feature explicitly
- aligns with deferred lighting, SSR, and volumetric needs

---

## New or evolved renderer components

### `GBufferTarget.cs`
A new MSAA MRT target for opaque world geometry, plus a resolve path to standard textures.

### `WorldGBufferRenderer.cs`
A new opaque-world renderer that writes material data, normals, and depth into the GBuffer but does not light anything.

### `DeferredLightingPass.cs`
The new opaque-lighting core. Reads the resolved GBuffer, SSAO, shadow map, irradiance cube, and specular environment. Outputs lit HDR color.

### `IblProbe.cs` upgrade
Current probe coverage is sufficient for sky and diffuse irradiance, but not for rough/specular reflections. Add:

- prefiltered specular cubemap
- roughness-to-mip convention shared by deferred lighting and SSR fallback

### `SsrPass.cs`
Half-resolution SSR with:

- view-space raymarch
- thickness heuristics
- binary refinement at hit
- roughness fade
- edge fade
- confidence-based fallback to specular cubemap
- optional effect-local temporal history once the base pass is stable

### `ContactShadowPass.cs`
Short-range screen-space raymarch toward the sun direction. Should be subtle and complement the shadow map, not replace it.

### `VolumetricFogPass.cs`
Quarter-resolution raymarch with:

- sparse shadow sampling
- fog density integration
- temporal accumulation
- bilateral/depth-aware upscale
- HDR composite

### `TemporalHistoryBuffer.cs`
Shared utility for effect-local temporal history (SSR, volumetrics, maybe contact shadows later).

---

## Reflection architecture

The current engine already has:

- `SkyCube`
- `IrradianceCube`

The hybrid renderer also needs:

- **SpecularCube** (prefiltered environment cubemap)

### Usage split
- diffuse ambient -> `IrradianceCube`
- glossy/specular fallback -> `SpecularCube`
- direct sky lookups for sky rendering only -> `SkyCube`
- SSR miss/uncertain hit -> blend toward `SpecularCube`

This is critical. SSR without a specular fallback always looks broken at screen edges and on off-screen reflections.

---

## Wet surface model

Wetness should be authored and carried as a proper material scalar.

### Wetness effects
- slightly darker diffuse response
- lower roughness
- stronger Fresnel
- stronger specular environment response
- stronger SSR weight

### Storage
- put `wetness` in `G2.a`

### Value
This is one of the cheapest ways to make the renderer feel dramatically more modern.

---

## Contact shadows

### Technique
Short view-space raymarch from the shaded pixel toward sun direction using depth.

### Purpose
- ground small geometry
- tighten near-contact shadowing
- complement the shadow map
- create a stronger "ray-traced" local-occlusion feel

### Constraint
Keep it short-range and subtle. Contact shadows should refine shadow-map results, not overtake them.

---

## Soft shadow filtering

### Recommendation
Use **PCSS-lite / improved PCF**, not an expensive full soft-shadow solver.

Suggested ingredients:
- rotated Poisson PCF
- blocker-aware softness approximation
- receiver-distance softening
- contact shadows for near sharpness

This layered approach is cheaper and looks richer than simply exploding the PCF radius.

---

## Volumetric fog

### Recommendation
Quarter-resolution fullscreen raymarch.

### Inputs
- depth
- shadow map
- fog parameters
- sun direction/color

### Process
- reconstruct view ray
- march 8–16 steps
- integrate density and shadowed in-scattering
- temporally accumulate
- bilateral/depth-aware upsample
- composite into HDR

### Why it matters
Volumetrics contribute disproportionate cinematic value:
- light shafts
- visible atmosphere
- shadowed fog depth
- stronger scale and mood

---

## MSAA strategy

The renderer should preserve the current project preference for crisp geometry edges.

### Practical compromise
- keep MSAA in the geometry/GBuffer stage
- resolve before fullscreen passes
- accept that deferred lighting, SSR, and volumetrics operate on resolved textures

This is the realistic GL 3.3 path.

---

## Phase-by-phase implementation order

### Phase 1 — Deferred foundation
- add `GBufferTarget`
- add `WorldGBufferRenderer`
- add `DeferredLightingPass`
- refactor `OpenGLFrameRenderer` to render opaque world through the new path
- achieve visual parity with the current opaque forward lighting

### Phase 2 — Specular environment probe
- upgrade `IblProbe` with prefiltered specular cubemap
- define roughness-to-mip sampling convention

### Phase 3 — SSR
- half-resolution
- cubemap fallback
- roughness-aware fade and miss handling
- no temporal yet until the base pass is stable

### Phase 4 — Wet materials
- add wetness parameter in runtime/material pipeline
- bias SSR and specular response for wet surfaces
- add sample wet content for visual validation

### Phase 5 — Contact shadows + shadow filtering
- add short-range contact shadow pass or modular term
- improve sun shadow filtering with PCSS-lite / better PCF

### Phase 6 — Volumetric fog
- quarter-resolution raymarch
- basic composite first
- temporal accumulation second

### Phase 7 — Effect-local temporal stabilization
- SSR history + reprojection
- volumetric history + reprojection
- careful validity rejection
- still no whole-frame TAA as the default AA strategy

### Phase 8 — Optional SDF VFX
- portals
- shields
- holograms
- local fog volumes

---

## Parallelizable sub-agent plan

The implementation can be divided into coordinated workstreams once the shared contracts are frozen.

## Contract freeze before coding
Before parallel work starts, the architect should freeze:
- GBuffer formats and channel usage
- texture unit assignments
- pass order
- shared uniform naming
- history-buffer ownership
- which paths stay forward vs deferred

Without that freeze, parallel work will collide.

### WS1 — Deferred foundation
**Owner:** renderer core  
**Scope:**
- `GBufferTarget`
- opaque GBuffer writing
- `DeferredLightingPass`
- `OpenGLFrameRenderer` pass graph rewire

**Dependencies:** none  
**Why first:** everything else depends on this frame graph.

### WS2 — Reflection infrastructure
**Owner:** environment / IBL  
**Scope:**
- `IblProbe` upgrade
- prefiltered specular cubemap
- reflection helper conventions

**Dependencies:** none  
**Can run in parallel with:** WS1

### WS3 — SSR
**Owner:** screen-space reflections  
**Scope:**
- half-res SSR pass
- confidence blending
- specular cubemap fallback
- optional temporal history after initial validation

**Dependencies:** WS1, WS2

### WS4 — Shadow quality
**Owner:** shadows  
**Scope:**
- improved PCF / PCSS-lite
- contact shadows

**Dependencies:** none  
**Can run in parallel with:** WS1, WS2

### WS5 — Volumetrics
**Owner:** atmosphere / post  
**Scope:**
- quarter-res volumetric fog pass
- upsample/composite
- optional temporal history for fog

**Dependencies:** WS1

### WS6 — Materials / wetness / content
**Owner:** materials + content authoring  
**Scope:**
- add wetness parameter through runtime/material contracts
- deferred-lighting wet response
- SSR wet weighting
- sample validation content

**Dependencies:** WS1, WS2

### Recommended merge order
1. WS1
2. WS2
3. WS4
4. WS3 + WS6
5. WS5
6. final integration / stabilization pass

---

## Performance targets
At 1080p on a mid-range GPU, aim roughly for:

- GBuffer opaque: `2.0–3.5 ms`
- SSAO: `0.7–1.5 ms`
- Deferred lighting: `0.5–1.2 ms`
- SSR half-res: `1.0–2.0 ms`
- Contact shadows: `0.4–0.9 ms`
- Volumetrics quarter-res: `1.0–2.5 ms`
- Bloom: `0.3–0.8 ms`
- Tonemap/post: `0.2–0.5 ms`

The exact numbers will vary, but this is the right budget mindset.

---

## Final recommendation
Do not pursue this as a gimmicky “fake ray tracing everywhere” effort.
Treat it as a disciplined renderer evolution:

- deferred opaque lighting
- SSR with proper fallback
- wet material response
- layered shadows
- volumetric atmosphere
- selective temporal stabilization

That is the most realistic way to achieve a strong cinematic pseudo-RTX style on legacy OpenGL 3.3 hardware in this codebase.
