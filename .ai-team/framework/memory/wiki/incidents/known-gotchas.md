---
id: known-gotchas
cat: incidents
rev: 2
created: 2026-04-26T12:00:00Z
updated: 2026-04-29T16:00:00Z
by: coordinator
tags: [gotchas, wpf, hwndhost, keyboard, axis-colors, shader, mvvm-toolkit, shooter, physics, glb]
summary: "Known recurring pitfalls in the shooter map editor and game runtime."
refs: [architecture/coordinate-contract, architecture/shooter-runtime, conventions/validation-workflow]
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

## open
- None.
