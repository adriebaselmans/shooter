---
id: pbr-upgrade-plan
cat: architecture
rev: 1
created: 2026-05-06T21:00:00Z
updated: 2026-05-06T21:00:00Z
by: developer
tags: [lighting, pbr, taa, gl, shooter, roadmap]
summary: "Plan for upgrading the Shooter renderer to full Physically Based Rendering and Temporal AA."
refs: [architecture/lighting-pipeline]
status: active
---

# PBR & TAA Upgrade Roadmap

This document outlines the planned multi-step upgrade to migrate the `Shooter.App` rendering pipeline from the current Blinn-Phong/heuristic-fallback model to a modern Physically Based Rendering (PBR) pipeline with Temporal Anti-Aliasing (TAA).

## Goal
Achieve a major graphics quality leap by introducing proper material response (metallic/roughness), accurate environment reflection (IBL split-sum), and stable sub-pixel edge smoothing (TAA).

---

## Phase 1: Core PBR Shading (Runtime)
**Objective**: Upgrade `Shaders.World.cs` and `LightingHeader` to use a true Cook-Torrance BRDF.

- **Lighting Math**: 
  - Replace specular power/intensity with GGX NDF, Smith geometry term, and Fresnel-Schlick.
  - Implement the metallic-roughness workflow (albedo is lerped with black based on metallic; F0 is lerped from 0.04 to albedo based on metallic).
- **Material Parameters**: 
  - Add `uMetallicMap` alongside `uRoughnessMap`.
  - Update `TextureCache.cs` to resolve `_metallic` companion maps.
- **IBL Upgrade**: 
  - The current 16x16 irradiance cube is for diffuse only. 
  - Implement a pre-filtered environment map (split-sum approximation) and a 2D BRDF integration LUT for specular IBL.

---

## Phase 2: Editor Authoring Support
**Objective**: Allow mappers to set and see PBR properties in the editor.

- **Data Model**: 
  - Update `BrushTexturing.cs` and the serialization DTOs to store `Metallic` alongside `Roughness`.
- **Editor UI**: 
  - Expose the Metallic slider and texture overrides in the Avalonia material property panel.
- **Runtime Sync**: 
  - Ensure the map parser (`MapSerializer.cs`) passes the new metallic values down to `WorldBrushFactory`.

---

## Phase 3: Material-Specific Normal Maps
**Objective**: Fully support explicitly authored normal maps without relying solely on heuristic relief.

- **Tangent Space**: 
  - Update `MeshGenerator.cs` to calculate proper Tangent/Bitangent vectors for brushes, instead of relying on the current fragment-shader gradient approximations.
- **Shader Update**: 
  - Modify `Shaders.World.cs` to use the explicit TBN matrix for `normalFromMap()`.
  - Ensure the fallback "detail normal from albedo" still works for legacy/unmapped surfaces.

---

## Phase 4: Temporal Anti-Aliasing (TAA)
**Objective**: Replace the current FXAA with a stable temporal history pass.

- **Motion Vectors**: 
  - Render a velocity buffer pass. Brushes are static, but the camera moves. Calculate exact pixel velocity from previous frame's View/Proj matrix.
- **Jitter**: 
  - Apply sub-pixel jitter to the projection matrix every frame (Halton sequence).
- **Resolve Pass**: 
  - Add a new fullscreen pass before Bloom/Tonemap that blends the current jittered frame with the history buffer, using neighborhood clamping to prevent ghosting.

---

## Risk Mitigation
- **Performance**: PBR is heavier. We will keep the shader branching clean and rely on the `TextureCache` to bind simple 1x1 fallback textures if maps are missing.
- **Complexity**: Do not merge these phases into one PR. We will tackle Phase 1 and Phase 2 together, verify stability, and then move to Tangents and TAA.
