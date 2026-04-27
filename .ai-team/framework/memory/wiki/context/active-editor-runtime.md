---
id: active-editor-runtime
cat: context
rev: 1
created: 2026-04-26T18:45:00Z
updated: 2026-04-26T18:45:00Z
by: coordinator
tags: [runtime, avalonia, macos, dotnet, startup]
summary: "Current active editor runtime is the Avalonia app in the main solution."
refs: [decisions/avalonia-primary-editor, architecture/module-boundaries]
status: active
---

## startup
- Build: `dotnet build shooter.slnx`
- Run: `dotnet run --project src/MapEditor.App.Avalonia/MapEditor.App.Avalonia.csproj`
- VS Code: `Launch Map Editor` and `run map editor` task.

## active solution
- `shooter.slnx` includes `MapEditor.App.Avalonia`, `MapEditor.Core`, `MapEditor.Formats`, `MapEditor.Rendering`.
- Tests are not in the active solution.

## current limits
- Build is verified on macOS.
- Residual warnings exist: nullable warnings in Avalonia viewport panel, one vulnerable transitive package.

## open
- Add a smoke-test checklist page once manual run/interaction parity is verified end-to-end.