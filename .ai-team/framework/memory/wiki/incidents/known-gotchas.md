---
id: known-gotchas
cat: incidents
rev: 5
created: 2026-04-26T12:00:00Z
updated: 2026-04-29T20:45:00Z
by: coordinator
tags: [gotchas, wpf, hwndhost, keyboard, axis-colors, shader, mvvm-toolkit, shooter, physics, glb, lighting, hdr, shadow, textures]
summary: "Known recurring pitfalls in the shooter map editor and game runtime."
refs: [architecture/coordinate-contract, architecture/shooter-runtime, architecture/lighting-pipeline, conventions/validation-workflow]
status: active
---

## Gotchas

### WPF + HwndHost keyboard routing
- Keyboard shortcuts do NOT auto-route when GL child HWND owns focus.
- Explicit shortcut router is required. Do not remove it.

### Axis color misdiagnosis
- Side/Top viewport bugs easy to misdiagnose as math when the real issue is axis color mismatch.
- Always verify color assignment before diving into math.

### Perspective floor guide colors
- Must match Top(XZ) convention: X=red, Z=blue. Non-negotiable.

### Perspective shader visibility
- Do not rely purely on scene lighting for editor brush visibility.
- Minimum editor tint must be preserved in the 3D solid shader.

### MVVM Toolkit command naming
- Generates `OpenFileCommand`/`SaveFileCommand`.
- Does NOT generate `OpenFileAsyncCommand`/`SaveFileAsyncCommand`.

### Parallel build/test
- Do not run `dotnet build` and `dotnet test` in parallel — transient file-lock issues.

### Player collider is a sphere, not a capsule
- `Player.Radius=0.4` is the collider; `HalfHeight=0.9` is camera/eye geometry only.
- Spawn snap-to-floor must use `Radius`. Using `HalfHeight` floats the player ~0.5m above ground.

### First-frame dt spike tunnels physics
- Loading GLB assets (textures + shader compile) makes the first rendered frame take hundreds of ms.
- Silk.NET reports that as the next `OnUpdate` dt; uncapped, gravity step can move > floor thickness in one frame and the player falls through.
- Mitigations (both kept): `Program.OnUpdate` clamps `dt` to `1/30s`; `Player.Update` clamps `Velocity.Y` to `-24 m/s` terminal velocity.

### Tracers vs view-space anchoring
- World-space tracers visibly desync from the gun during recoil.
- Hitscan feedback now uses a view-space muzzle flash. Tracer system kept compiled but unused.

### Per-weapon visual tuning lives at the trigger site
- Flash anchor + scale + recoil strength are picked in the `WeaponKind` switch in `Program.cs` and on `WeaponDef.RecoilStrength`.
- When a flash looks misaligned, change those four numbers, not the renderer.

### glTF asset filenames are authoritative
- Pack ships `rocketlaucher.glb` (typo). Code references must match exactly; do not silently rename.

### HDR pipeline: default framebuffer is written exactly twice
- PostFx pass writes the tone-mapped scene; HudRenderer writes UI on top. Anything else that writes the default FB is a bug — it will appear unblended next to the HUD or be tone-mapped twice.
- All world-space lit / decal / effect renderers must target the HdrTarget FBO.

### Sky depth-trick requires GL_LEQUAL
- The sky cube uses `gl_Position.xyww` so depth = 1. The fresh-cleared depth buffer is also 1, so `GL_LEQUAL` is required for the sky to draw at all. SkyRenderer.Draw flips DepthFunc to LEQUAL during the draw and back to LESS afterwards.
- Sky also needs `DepthMask(false)` so it does not lock the buffer at 1.0 and prevent opaque scene depth writes.

### Hardware PCF needs all three texture parameters
- `samplerShadow` requires `TextureCompareMode = CompareRefToTexture` AND `CompareFunc = Lequal` AND a `DEPTH_COMPONENT*` internal format. Forgetting any of the three either produces compile-time linker errors or always-1.0 / always-0.0 lookups at runtime.

### View-space objects can't sample the world shadow map
- The viewmodel renders in view-space (its world position in the shader is not the player's world). Sampling the world-space shadow map for it produces nonsense. `TexturedModelRenderer.BeginPass(receiveShadows: false)` must be used for view-space passes.

### Additive HDR effects must use HDR-magnitude colors
- The muzzle flash is multiplied by 6.0 in the fragment shader so its bloom and tone-mapped result both read as bright. Non-boosted additive effects look correctly bright in the buffer but tone-map down to mid-gray.

### Bloom resize is cheap; call it whenever framebuffer changes
- HdrTarget and Bloom both track size and short-circuit when matching. Both are resized in OnRender as well as via FramebufferResize callback to handle retina + first-frame edge cases.

### Shooter brush textures treat `material_name` as an image path only when it looks like one
- Shooter.App does **not** resolve symbolic editor material keys like `brick/red_standard` into files yet.
- The runtime textured path activates only when `material_name` ends with a supported image extension (`.png`, `.jpg`, `.jpeg`, `.bmp`).
- Non-image material names are expected to keep using hashed tint fallback.

### Missing brush texture files must stay non-fatal
- Dust map intentionally relies on runtime fallback behavior: if a referenced image is missing, the map should still boot and render with tint for those brushes.
- Do not convert texture-resolution failures into hard map-load exceptions unless there is a separate asset-validation mode.

### Large brush textures need authored `surface_mappings`
- Default texture-locked UVs scale by world size / 64. On very large floors/walls this often yields too few repeats; on tiny crates it yields giant blurred stamp textures.
- Before changing runtime UV code, prefer authoring `surface_mappings.scale` in the `.shmap`. The Dust map polish pass used this to fix wall/floor readability without renderer changes.

### Box top/bottom winding matters more than map-side seam hacks
- A persistent floor/wall disconnection artifact in Shooter.App was eventually traced to `MeshGenerator.GenerateBox`: the top and bottom faces were wound opposite their normals.
- Because the whole shooter world is box-based, this manifested like a global floor/wall seam problem even when map transforms were correct.
- Before piling on map-side sinks/plinths/debug toggles, verify generated face winding and triangle orientation for the primitive itself.

### Tall wall bottoms should overlap the ground slightly
- When a floor top and wall bottom meet exactly coplanar, lighting/shadow/texture contrast can make the wall feel like it floats even when the geometry mathematically touches.
- A tiny overlap can still help robustness, but it is not a substitute for correct primitive face winding.

## open
- None.
