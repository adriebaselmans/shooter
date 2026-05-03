---
id: renderer-abstraction-and-metal-bootstrap
cat: decisions
rev: 1
created: 2026-05-01T13:05:00Z
updated: 2026-05-01T13:05:00Z
by: architect
tags: [renderer, abstraction, metal, opengl, macos, backend, shooter, decisions]
summary: "Shooter.App adopts a backend abstraction with OpenGL as the stable implementation and a first concrete Metal bootstrap backend on macOS."
refs: [architecture/shooter-runtime]
status: active
---

## Decision
Introduce a backend abstraction at the renderer/frame lifecycle level.

- `Program` keeps gameplay, input, update, and map loading.
- Rendering lifecycle is delegated to `IRenderBackend`.
- The old feature-complete OpenGL path is wrapped as `OpenGLRenderBackend` and remains the default stable backend.
- The first non-GL backend is a concrete macOS `MetalBootstrapBackend` that attaches a `CAMetalLayer` to the existing Silk Cocoa window and clear/presents frames.

## Why this level of abstraction
A full backend-neutral render graph / resource model rewrite is too large for one step.
Cutting the seam at backend selection, initialization, resize, render, and dispose is enough to remove direct Program-level GL ownership and prepare for later backend feature migration.

## Why Metal first
For macOS, Metal is the native modern graphics API. A Vulkan-first plan would still need MoltenVK or another translation path, while Metal provides a cleaner macOS-native target for backend bring-up.

## Explicit non-goal of this phase
This phase does **not** deliver full visual feature parity between OpenGL and Metal. The Metal backend is bootstrap-only (clear/present), while OpenGL remains the production gameplay renderer.
