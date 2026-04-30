---
id: glb-asset-pipeline
cat: decisions
rev: 1
created: 2026-04-29T16:00:00Z
updated: 2026-04-29T16:00:00Z
by: developer
tags: [assets, glb, gltf, sharpgltf, fbx, assimp, decision]
summary: "Shooter.App uses SharpGLTF/GLB for weapons; FBX/Assimp dropped."
refs: [architecture/shooter-runtime]
status: active
---

## Decision
Weapon and projectile models load from `.glb` via `SharpGLTF.Runtime`. FBX/Assimp removed.

## Rationale
- **No native deps**: Assimp shipped a per-platform `libassimp` (Ultz transitive). GLB is fully managed.
- **Embedded PBR textures**: each glTF primitive carries baseColor texture bytes + factor; rendered via `TexturedModelFrag`.
- **Open standard, deterministic**: no driver/version drift in scene parsing.
- **Per-primitive structure** maps cleanly to one VAO + one texture upload each.

## Pipeline
1. `ModelData.TryLoad(path)` → list of `PrimitiveData` (interleaved pos+normal+uv, baseColor png/jpeg bytes, factor).
2. `ModelData.AlignBarrelToForward(targetForwardLength)` — orients the longest axis to `-Z`, anchors the front face at the model origin so the barrel tip = local origin.
3. `GpuModel.Upload(gl, data)` — builds `GpuPrimitive` per primitive: `GlMesh` (8 floats/vertex) + `GlTexture` from PNG/JPEG via `StbImageSharp`.
4. `TexturedModelRenderer` draws the model with shared Phong+baseColor shader.

## Asset slots
- AK-47: `assets/StylooGunsAssetPack/GLB/ak47.glb`
- Shotgun: `assets/StylooGunsAssetPack/GLB/shotgun.glb`
- Rocket Launcher: `assets/StylooGunsAssetPack/GLB/rocketlaucher.glb` (typo in pack)
- Rocket projectile: `assets/StylooGunsAssetPack/GLB/quadrocket.glb`

## Trade-offs accepted
- One-time first-frame upload spike (textures + shader compile). Mitigated by clamping `dt` in the game loop.
