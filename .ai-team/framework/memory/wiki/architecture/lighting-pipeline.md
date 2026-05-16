---
id: lighting-pipeline
cat: architecture
rev: 2
created: 2026-04-29T17:40:00Z
updated: 2026-05-06T22:30:00Z
by: developer
tags: [lighting, hdr, shadow, ibl, bloom, aces, postfx, gl, shooter, deferred, ssr, msaa]
summary: "Shooter.App current OpenGL lighting pipeline: hybrid deferred opaque + forward specials, HDR, shadows, SSR, bloom, ACES."
refs: [architecture/shooter-runtime, decisions/lighting-pipeline]
status: active
---

## Pipeline (current OpenGL frame)
1. **Shadow depth pass** — `ShadowMap.RenderPass` over world brushes into a 3072² depth map with hardware PCF support. Current tuning uses back-face cull plus a stronger polygon offset than the earlier pass to reduce self-shadow acne.
2. **GBuffer pass** — standard opaque world brushes render into a **4x MSAA** GBuffer.
3. **GBuffer resolve** — multisampled GBuffer resolves into regular 2D textures.
4. **SSAO** — fullscreen SSAO over resolved depth + normal when enabled.
5. **Contact shadows** — screen-space contact-shadow visibility over resolved depth + normal when shadows are enabled. Current defaults are tighter/less aggressive to avoid diagonal self-shadow patterns on flat walls/floors.
6. **HDR scene target bind** — main scene uses a **4x MSAA HDR target** (`HdrTarget`).
7. **Sky** — `SkyRenderer` draws the analytic atmosphere into HDR.
8. **Deferred lighting** — fullscreen light pass consumes GBuffer + SSAO + contact shadows + shadow map + IBL to shade standard opaque world surfaces.
9. **Forward opaque specials** — non-standard opaque materials / pickups remain on forward paths.
10. **HDR resolve** — multisampled HDR target resolves into regular 2D textures.
11. **SSR** — screen-space reflections run over the resolved HDR scene with environment fallback and temporal stabilization.
12. **Water** — specialized forward water pass samples the resolved scene color/depth for refraction and reflection.
13. **Decals / scorch / tracers / rockets / particles / viewmodel / muzzle flash** — rendered into the HDR target after opaque lighting.
14. **HDR resolve** — final scene resolve for post.
15. **Bloom** — bloom runs over resolved HDR color when enabled.
16. **Auto-exposure** — average luminance drives exposure adaptation when enabled.
17. **PostFx** — tonemap / grade / composite to the default framebuffer.
18. **HUD** — final LDR menu/HUD pass on top.

## Removed / no longer active
- **Volumetric fog** was implemented, evaluated, then removed because it made the scene feel dull / less colorful.
- **TAA and FXAA** remain removed; hardware MSAA is the chosen AA strategy.

## MSAA
- `Program.cs` requests **4 samples** from the GL context.
- `GBufferTarget` uses `Texture2DMultisample` attachments with **4 samples**.
- `HdrTarget` also uses `Texture2DMultisample` attachments with **4 samples**.
- Both targets are explicitly resolved before post-processing passes that need normal 2D textures.

## Relief / material detail
- Relief no longer lives only in the forward path.
- The deferred standard-opaque path now binds height maps, texel size, enable flag, and relief scale into the GBuffer shader.
- Relief strength now directly affects visible normal contribution and crevice darkening rather than only weak internal slope math.
- Current default relief strength: `0.090`.

## Shadow sampling
- `pcfShadow(worldPos, n)` lives in shared `LightingHeader`.
- Recent tuning increased the slope-dependent bias and shadow-map polygon offset to suppress wall/floor diagonal acne.
- Contact shadows were also shortened and biased more conservatively.

## Single source of truth
`Game/LightingEnvironment.cs` owns sun direction, sun color/intensity, sky parameters, fog/grading settings, SSAO, bloom, auto-exposure, shadows, and relief toggles/strength.

## Texture units (major lit shader bindings)
- 0: baseColor
- 4: shadow map
- 5: irradiance cube
- 8: sky cube
- 15: specular environment cube
- 14: normal map (world path)
- 7: height map (world path)
