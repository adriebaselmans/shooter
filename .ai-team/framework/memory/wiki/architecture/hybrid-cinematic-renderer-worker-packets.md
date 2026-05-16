---
id: hybrid-cinematic-renderer-worker-packets
cat: architecture
rev: 1
created: 2026-05-06T23:05:00Z
updated: 2026-05-06T23:05:00Z
by: coordinator
tags: [renderer, opengl, gl33, deferred, sub-agents, ws1, ws2, ws4, shooter]
summary: "Concrete worker packets for the first parallel implementation wave of the GL 3.3 hybrid cinematic renderer roadmap."
refs: [architecture/hybrid-cinematic-renderer-plan, architecture/hybrid-cinematic-renderer-workstreams]
status: active
---

# Parallel Worker Packets — Wave 1

This packet defines the first three parallel workers to start immediately after contract freeze.

## Worker A — WS1 Deferred Foundation

### Goal
Move opaque world rendering onto a new deferred foundation without breaking the current runtime.

### Scope
- finalize `GBufferTarget`
- wire `WorldGBufferRenderer` into the frame graph
- implement real `DeferredLightingPass` parity with current opaque PBR/shadow/IBL lighting
- add safe runtime toggle / fallback while parity is incomplete
- keep water, viewmodel, particles, and forward specials on existing paths

### Files owned first
- `src/Shooter.App/Render/GBufferTarget.cs`
- `src/Shooter.App/Render/WorldGBufferRenderer.cs`
- `src/Shooter.App/Render/DeferredLightingPass.cs`
- `src/Shooter.App/Render/Shaders.Deferred.cs`
- `src/Shooter.App/RenderSystem/OpenGLFrameRenderer.cs`
- `src/Shooter.App/RenderSystem/OpenGLPostProcessResources.cs`
- `src/Shooter.App/RenderSystem/OpenGLSceneResources.cs`

### Must not touch initially
- SSR implementation
- volumetrics
- IBL prefilter implementation
- water shader behavior except compatibility fixes

### Deliverable
Opaque world can render through deferred path with visual parity close enough to compare against the existing forward path.

### Validation
- `dotnet build shooter.slnx -nologo`
- smoke startup on `dust.shmap`
- compare deferred opaque world against current forward reference

---

## Worker B — WS2 Reflection Infrastructure

### Goal
Upgrade environment lighting so glossy/specular reflections have the right data source for future SSR fallback and wet materials.

### Scope
- extend `IblProbe` with prefiltered specular cubemap generation
- define roughness-to-mip convention
- expose shared shader contract for specular environment lookup
- preserve current sky + irradiance behavior

### Files owned first
- `src/Shooter.App/Render/IblProbe.cs`
- `src/Shooter.App/Render/Shaders.Sky.cs`
- `src/Shooter.App/Render/Shaders.cs`
- `src/Shooter.App/Render/WorldRenderer.cs` (lighting bindings only, if needed)

### Must not touch initially
- main frame graph
- SSR pass implementation
- wetness/material authoring

### Deliverable
A stable specular cubemap path exists and can be sampled by roughness level.

### Validation
- `dotnet build shooter.slnx -nologo`
- probe generation still succeeds at startup
- no regressions in sky or irradiance-based ambient

---

## Worker C — WS4 Shadow Quality

### Goal
Improve grounded shadow quality while staying mostly independent from the deferred foundation.

### Scope
- refine shadow filtering (better PCF / PCSS-lite)
- add short-range contact shadow prototype in a modular pass or helper
- keep the initial implementation compatible with both current forward lighting and future deferred integration

### Files owned first
- `src/Shooter.App/Render/ShadowMap.cs`
- `src/Shooter.App/Render/Shaders.cs`
- `src/Shooter.App/Render/Shaders.Deferred.cs` (only if shared helper needed)
- `src/Shooter.App/Render/ContactShadowPass.cs` (new)

### Must not touch initially
- GBuffer layout
- SSR implementation
- volumetrics

### Deliverable
Shadow filtering improves and a contact-shadow term exists behind a controlled integration seam.

### Validation
- `dotnet build shooter.slnx -nologo`
- no catastrophic self-shadow artifacts
- direct lighting still stable on `dust.shmap`

---

# Integration notes

## Dependency summary
- Worker A can proceed immediately.
- Worker B can proceed immediately.
- Worker C can proceed immediately.
- SSR and volumetrics wait until Worker A lands.
- Wetness waits until Worker A + B land.

## Merge order for Wave 1
1. Worker A base scaffold and frame graph hooks
2. Worker B probe upgrade
3. Worker C shadow quality branch
4. Integration review pass

## Shared review checklist
- texture units do not collide
- fullscreen passes use resolved textures only
- no duplicate lighting of opaque geometry
- water remains on its specialized path
- build remains green throughout
