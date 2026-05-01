---
id: lighting-pipeline
cat: decisions
rev: 1
created: 2026-04-29T17:40:00Z
updated: 2026-04-29T17:40:00Z
by: architect
tags: [lighting, hdr, sky, bloom, ibl, aces, decisions]
summary: "Resolutions of Q1..Q4 from req-shooter-lighting-pass: sky model, bloom kernel, IBL sizes, muzzle-flash HDR boost."
refs: [architecture/lighting-pipeline]
status: active
---

## Q1 — Sky model
**Choice:** Hand-tuned analytic atmosphere (blue zenith + warm horizon + Mie sun glow + below-horizon ground).
**Rejected:** Preetham, Hosek-Wilkie.
**Rationale:** Full Preetham/Hosek-Wilkie need turbidity-dependent coefficient tables and tan/exp branches that are easy to mistune in portable GLSL. A 30-line analytic model with Mie sun glow gives the same perceptual result and is debuggable.

## Q2 — Bloom kernel
**Choice:** Five-mip Gaussian downsample/upsample chain (Karis 13-tap down, 9-tap tent up).
**Rejected:** Dual-Kawase.
**Rationale:** Standard, robust, easy to debug. Dual-Kawase saves ~0.3 ms — irrelevant at our scene complexity.

## Q3 — IBL probe sizes
**Choice:** Sky cube 64², irradiance cube 16², 64 hemisphere samples per output texel.
**Rationale:** Diffuse irradiance is band-limited; 16² + cubemap bilinear filtering is sufficient. 64 stratified cosine-weighted samples converges visually. One-shot at startup; rebuild hooks present but unused.

## Q4 — Muzzle flash HDR boost
**Choice:** Multiply muzzle-flash final color by 6.0; scorch core stays HDR-low (no boost beyond ash/sooty palette).
**Rationale:** ACES compresses high values toward white but preserves brightness ordering, so an HDR boost at the source survives tone mapping with a clean bloom halo, instead of clamping additive blend at 1.0 pre-tonemap and producing a flat white smear.
