---
id: known-gotchas
cat: incidents
rev: 6
created: 2026-04-26T12:00:00Z
updated: 2026-05-06T22:30:00Z
by: developer
tags: [gotchas, wpf, hwndhost, keyboard, axis-colors, shader, mvvm-toolkit, shooter, physics, glb, lighting, hdr, shadow, textures, deferred, msaa]
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
- Loading GLB assets / textures / shader compile can make the first rendered frame take hundreds of ms.
- Silk.NET reports that as the next `OnUpdate` dt; uncapped, gravity step can move through the floor in one frame.
- Mitigations are kept: `Program.OnUpdate` clamps `dt` to `1/30s`; `Player.Update` clamps `Velocity.Y` to `-24 m/s`.

### Tracers vs view-space anchoring
- World-space tracers visibly desync from the gun during recoil.
- Hitscan feedback now uses a view-space muzzle flash. Tracer system stayed, but hitscan presentation moved on.

### View-space objects can't sample the world shadow map
- The viewmodel renders in view-space, not real world space.
- Sampling the world shadow map for it produces nonsense. Keep `receiveShadows=false` for view-space passes.

### HDR pipeline still relies on explicit offscreen targets
- PostFx writes the tone-mapped scene; HudRenderer writes UI on top.
- Do not reintroduce direct default-framebuffer world draws.

### Hardware MSAA is active, but it does not solve all aliasing
- Current renderer still uses **4x MSAA** on both the GBuffer and the HDR scene target.
- Remaining jaggies can still come from specular aliasing, relief/normal-map high-frequency shading, thin geometry, or post-resolve passes.
- Do not misdiagnose every jagged edge as “MSAA is off.”

### Volumetric fog was intentionally removed
- A quarter-res volumetric fog/light-scatter pass existed and was menu-toggleable for a moment.
- It was removed because it made the image feel dull / less colorful.
- If atmospheric fog returns, it needs a different artistic balance and should not silently resurrect the old pass.

### Deferred path can silently drop material features if GBuffer bindings lag behind
- Relief recently regressed because the forward world shader still supported it, but the deferred standard-opaque GBuffer path was not binding/using height-map inputs.
- When a runtime toggle seems dead, always verify both forward and deferred paths.

### Flat surfaces can reveal diagonal shadow self-occlusion
- Walls/floors built from two triangles can show a hidden diagonal split when shadow bias/contact-shadow settings are too aggressive.
- Current mitigation is more conservative bias + polygon offset + tighter contact-shadow defaults.
- If the artifact returns, isolate whether it comes from the sun shadow map or the screen-space contact-shadow pass before changing material/UV logic.

### Shooter brush textures treat `material_name` as an image path only when it looks like one
- Shooter.App does **not** resolve symbolic editor material keys into files yet.
- The runtime textured path activates only when `material_name` ends with a supported image extension.
- Non-image material names are expected to keep using hashed tint fallback.

### Missing brush texture files must stay non-fatal
- If a referenced image is missing, the map should still boot and render with tint for those brushes.
- Do not turn texture-resolution failures into hard map-load exceptions unless there is a separate asset-validation mode.

### Large brush textures need authored `surface_mappings`
- Default texture-locked UVs scale by world size / 64.
- On very large floors/walls this often yields too few repeats; on tiny props it yields giant blurred stamps.
- Prefer authoring `surface_mappings.scale` in the map before changing runtime UV code.

### Box top/bottom winding matters more than map-side seam hacks
- A persistent floor/wall disconnection artifact in Shooter.App was eventually traced to `MeshGenerator.GenerateBox`: top and bottom faces were wound opposite their normals.
- Before piling on map-side overlap/plinth/debug hacks, verify generated face winding and triangle orientation for the primitive itself.

## open
- None.
