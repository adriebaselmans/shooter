---
id: current-behavior
cat: context
rev: 10
created: 2026-04-26T12:00:00Z
updated: 2026-05-06T22:30:00Z
by: developer
tags: [behavior, ux, editor, viewports, shortcuts, primitives, shooter, gameplay, lighting, textures, metal, deferred, msaa]
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
- `.shmap` files saved by the editor (1.4.0) load without code changes.
- Shooter.App treats image-backed brush `material_name` values as optional diffuse texture paths; missing files must fall back to tint rendering rather than aborting load.

## Must stay true (Shooter.App lighting)
- Standard opaque world surfaces render through the hybrid deferred OpenGL path; special cases stay on forward/specialized paths.
- All main world-space geometry renders into HDR offscreen targets, not directly to the default framebuffer.
- The final default framebuffer write sequence is still post first, HUD last.
- Sky is procedural analytic, driven by `LightingEnvironment.SunDirection` + `Turbidity`.
- Sun casts soft shadows; recent tuning should avoid obvious diagonal self-shadow patterns on flat walls/floors.
- Ambient is image-based: comes from sky-derived cubemaps, not a flat constant.
- Bright effects bloom; HUD remains crisp and unaffected by tone mapping.
- Hardware **4x MSAA** remains the active anti-aliasing strategy.
- TAA / FXAA remain removed.
- Volumetric fog is currently **not** part of the active renderer.
- Brush relief toggle and relief-strength slider must visibly affect the main world render; current default relief strength is `0.090`.
- Brush material behavior can be authored directly in the editor and persisted into `.shmap` through a brush-level `material_properties` block.
- Shooter.App still supports runtime companion maps by filename convention, while authored material properties define intended behavior directly for Standard/Water/Lava brushes.
- Distant outdoor geometry still uses the lighter dust-style haze/fog baked into the main lighting/post behavior; the removed volumetric pass must not silently return.
- `dust.shmap` still includes a water-authored demo brush to verify the workflow visually.

## Must stay true (Shooter.App Metal/backend remnants)
- Metal/backend-abstraction code may remain in-repo, but current visual iteration/reference behavior is the OpenGL renderer.
- Existing OpenGL behavior must not regress while backend leftovers remain present.

## open
- None.
