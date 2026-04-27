---
id: avalonia-primary-editor
cat: decisions
rev: 1
created: 2026-04-26T18:45:00Z
updated: 2026-04-26T18:45:00Z
by: coordinator
tags: [avalonia, editor, ui-migration, cross-platform, dotnet]
summary: "Avalonia is the primary editor shell and startup path going forward."
refs: [architecture/module-boundaries, architecture/viewport-hosting, context/active-editor-runtime]
status: active
---

## decision
- Primary desktop editor: `MapEditor.App.Avalonia`.
- WPF app remains legacy/reference code, not the active solution startup.

## rationale
- Preserves XAML + MVVM migration path.
- Keeps editor-specific shell behavior without a renderer rewrite.
- Allows multi-platform UI path while preserving OpenGL viewport semantics.

## consequences
- Shared editor logic must stay UI-neutral.
- New shell/runtime work targets Avalonia first.
- Validation baseline is `dotnet build shooter.slnx` on the active solution.

## open
- Decide whether legacy WPF project stays long-term or is archived later.