---
id: current-behavior
cat: context
rev: 9
created: 2026-04-26T12:00:00Z
updated: 2026-05-01T23:50:00Z
by: coordinator
tags: [behavior, ux, editor, viewports, shortcuts, primitives, shooter, gameplay, lighting, textures, metal]
summary: "Stable user-facing behaviors that must not regress."
status: active
---

## Must stay true
- Window behaves like a normal desktop app (movable, maximize, close).
- Orthographic views show logical projections matching 3D world axes.
- 3D view shows readable translucent brush colors matching orthographic color language.
- Selected brushes movable by direct manipulation in orthographic editing.
- Brush creation visible in realtime in orthographic and 3D views.
- Open/save/menu/toolbar/shortcut flows work.
- Ctrl-based shortcuts work even when a GL viewport has focus.
- Primitive creation uses toolbar buttons (Box, Cylinder, Cone, Wedge), not a dropdown.
- Primitive buttons are mutually exclusive single-use toggles: activate → create mode; click active again → select mode; completing brush creation → auto-return to select mode.

## Must stay true (Shooter.App)
- Player spawns standing on the floor; never drops on first frame.
- Player cannot tunnel through floor brushes during stutter or first-frame asset upload.
- World FOV and viewmodel FOV match (`75°`); projectile spawn point matches the visible barrel tip through recoil.
- Hitscan fires (AK-47, shotgun) emit a view-space muzzle flash anchored over the visible nozzle, no yellow tracer line.
- Rocket launcher spawns a visible textured rocket model along the firing direction.
- Rocket detonation leaves one large noisy scorch smudge, not a bullet-hole ring.
- All 3 weapons selectable (1/2/3) from spawn with starter ammo.
- `.shmap` files saved by the editor (1.3.0) load without code changes.
- Shooter.App treats image-backed brush `material_name` values as optional diffuse texture paths; missing files must fall back to tint rendering rather than aborting load.

## Must stay true (Shooter.App lighting)
- All world-space geometry renders into an HDR (RGBA16F) offscreen target, not the default framebuffer.
- The default framebuffer is written exactly twice per frame: PostFx tone-map, then HUD.
- Sky is procedural analytic (no asset), driven by `LightingEnvironment.SunDirection` + `Turbidity`.
- Sun casts a soft (PCF) shadow from world brushes onto the floor and other brushes; pickups and rockets receive the shadow; the weapon viewmodel does not.
- Ambient is image-based: comes from a convolved sky cubemap, not a flat constant.
- Bright effects (muzzle flash, scorch core) bloom; non-emissive surfaces do not.
- Final image is ACES tone-mapped and gamma-encoded; exposure is a single uniform on the post pass unless auto-exposure is enabled.
- Static world brushes may sample diffuse/albedo textures through their authored UVs; pickups remain untextured self-illuminated cubes.
- HUD remains crisp, LDR, unaffected by exposure / bloom / tone map.
- The visual baseline is intentionally less over-bright than before: exposure, ambient, sun, and bloom are tuned to preserve stronger contrast and more readable highlights.
- Textured world surfaces now receive heuristic roughness/specular/detail-normal response even when dedicated normal maps are not yet authored.
- Brush material behavior can now be authored directly in the editor and persisted into `.shmap` through a brush-level `material_properties` block.
- Shooter.App still supports runtime companion maps by filename convention, but authored material properties now define intended behavior directly for Standard/Water/Lava brushes.
- Distant outdoor geometry picks up subtle dust-style haze/fog, and the final frame receives mild color grading plus vignette.
- Rocket smoke, explosion dust/debris, and muzzle smoke particles now add lightweight motion polish.
- `dust.shmap` now includes a water-authored demo brush to verify the workflow visually.

## Must stay true (Shooter.App Metal backend)
- `Shooter.App --backend=metal` initializes a concrete Metal backend on macOS instead of routing through OpenGL.
- The Metal backend renders world/models into HDR scene textures, runs directional shadowing, SSAO, bloom, and tone mapping, then draws HUD last.
- Metal decals, scorch marks, and muzzle flash render in the HDR scene path rather than being omitted.
- Metal also includes a hybrid path-traced indirect-lighting layer that accumulates while the camera is stable and resets on movement/resize.
- Metal remains compatible with the backend abstraction and does not change the default OpenGL startup path.

## open
- None.
