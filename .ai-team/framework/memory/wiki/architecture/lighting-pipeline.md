---
id: lighting-pipeline
cat: architecture
rev: 1
created: 2026-04-29T17:40:00Z
updated: 2026-04-29T17:40:00Z
by: developer
tags: [lighting, hdr, shadow, ibl, bloom, aces, postfx, gl, shooter]
summary: "Shooter.App HDR-linear lighting pipeline: sky, IBL, shadows, bloom, ACES."
refs: [architecture/shooter-runtime, decisions/lighting-pipeline]
status: active
---

## Pipeline (Program.OnRender)
1. **Shadow depth pass** — `ShadowMap.RenderPass` over world brushes only. 2048² GL_DEPTH_COMPONENT24, hardware PCF (`sampler2DShadow` + `GL_COMPARE_REF_TO_TEXTURE`), front-face cull, glPolygonOffset(2.0, 4.0).
2. **Bind HDR target** — RGBA16F + depth-stencil renderbuffer at framebuffer size; recreated on resize.
3. **Sky** — `SkyRenderer` draws unit cube with `gl_Position.xyww` (depth=1) at GL_LEQUAL, depth-mask off. Analytic atmosphere: blue zenith + warm horizon + Mie sun glow + below-horizon ground.
4. **World + pickups** — `WorldRenderer.Draw` with shadow + IBL + Lambert. Same shader for both; pickups bump `uSelfIllum=0.45`.
5. **Decals, scorches, tracers** — alpha/additive into HDR.
6. **Rocket** — `RocketRenderer` via `TexturedModelRenderer`, receives shadows.
7. **Viewmodel** — `WeaponViewmodelRenderer` via `TexturedModelRenderer` with `receiveShadows=false` (view-space; shadow lookup would be wrong).
8. **Muzzle flash** — additive, HDR_BOOST=6.0 to survive ACES.
9. **Bloom** — `Bloom.Run`: threshold @ 0.9 with knee → 5 downsamples → 4 additive upsamples (tent filter).
10. **PostFx** — `PostFx.Draw` to default framebuffer: HDR + bloom × 0.05 → ×Exposure → ACES → gamma 1/2.2.
11. **HUD** — `HudRenderer` LDR pass on default framebuffer, unaffected by exposure.

## IBL probe (one-shot)
- Sky → 64² cubemap (6 face quad draws with face basis uniforms).
- Convolve → 16² irradiance cubemap, 64 cosine-weighted hemisphere samples per output texel.
- Lit shaders sample `iblAmbient(N) = texture(uIrradiance, N).rgb * uIrradianceIntensity`.

## Shadow sampling
- `pcfShadow(worldPos, n)` in shared `LightingHeader`. Slope-scaled bias `max(0.0008*(1-N·L), 0.0003)`. 3×3 kernel averaged. Outside frustum returns 1.0.
- ShadowMap built per frame: ortho 30 m half-extent centered on player, texel-snapped.

## Single source of truth
`Game/LightingEnvironment.cs` — SunDirection, SunColor, SunIntensity (HDR), Turbidity, GroundAlbedo, Exposure, IrradianceIntensity. Threaded into every renderer.

## Texture units (lit shaders)
- 0: baseColor (textured models)
- 4: shadow map (`sampler2DShadow`)
- 5: irradiance cube (`samplerCube`)
