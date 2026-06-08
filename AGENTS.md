# RexTools — AGENTS.md

**Unity 6 UPM package** (6000.0). Not a Unity project — root `package.json` is the manifest. Install via UPM git URL.

## Tool entry points

All tools open via `Tools/Rex Tools/<Tool Name>` (see `readme.md` for full list). Presets via `Assets/Create/RexTools/Internal/`. `[InitializeOnLoad]` shader graph tools activate automatically.

## Architecture

- **Two assemblies:** `Editor/RexTools.Editor.asmdef` (Editor-only, references URP + ShaderGraph) and `Runtime/RexTools.Runtime.asmdef` (standalone)
- **UI must use UI Toolkit** (UXML + USS). Reuse `.rex-*` classes from `Editor/RexToolsStyles.uss`. Follow layout patterns in `design.md` and C# styling rules in [ui-toolkit-guide.md](file:///p:/Personal/00%20Unity/03%20RexTools/RexTools/RexTools-Unity6/docs/ui-toolkit-guide.md) — do not use IMGUI for new work
- **Conditional:** `REX_URP` is defined when URP is present; guard URP-only post-processing with `#if REX_URP`
- **ShaderGraph tools** (`ShaderGraphSearch`, `ShaderGraphOrganizer`) use `[InitializeOnLoad]` + reflection on internal `Unity.ShaderGraph.Editor` types
- **Preset save/load:** Use `RexPresetManager.CreatePresetButtons()` / `SavePreset()` / `ShowPresetSelector()` from `Editor/Core/RexPresetManager.cs`
- **Icons:** 1x in `Editor/Icons/{Name}.png`, 2x in `Editor/Icons/{Name}@2x.png`
- **Namespaces** follow folder paths (e.g. `RexTools.BatchMaterialEditor.Editor.Tabs`, `RexTools.AutoLODSetup.Editor`)
- **Single-file tools** are the norm; most tools are one large `.cs` file

## Commands

No test, lint, typecheck, or build tooling exists. No CI/CD. No pre-commit hooks.

## Workflow conventions

- **Commits:** `feat(tool-name): description` (conventional commits). Always stage and commit corresponding Unity `.meta` files alongside any new/modified assets or scripts.
- **Changelog:** Add entries under `[Unreleased]` in `CHANGELOG.md`. On release, rename to version, update `package.json` version, add blank `[Unreleased]` header. See `.agent/workflows/changelog.md`
- **Documentation:** After shipping a tool, update `readme.md` and `CHANGELOG.md`. See `.agent/workflows/document-tool.md`
