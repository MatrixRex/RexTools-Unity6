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
- **Architecture files:** Smaller, utility-focused tools can remain single-file. However, larger/complex tools should split visual layout (.uxml), styling (.uss), and code-behind (.cs) to improve readability and support future feature expansions.

## Commands

No test, lint, typecheck, or build tooling exists. No CI/CD. No pre-commit hooks.

## Workflow conventions

- **Feature Branching:** Never work directly on `main` when developing a new feature. Always create and work in a temporary feature branch (e.g. `feature/<name>`). When work is finished, explicitly ask the user for confirmation to merge into `main` and/or bump the version. Do not commit, merge, clear the branch, or bump version without explicit user confirmation. If user agrees to both, do both; if user agrees to none, do none. Follow `.agent/workflows/feature-workflow.md`.
- **Commits:** `feat(tool-name): description` (conventional commits). Do NOT automatically stage or commit to Git after each task is done. ONLY stage and commit when the user explicitly asks for it. When doing so, always stage and commit corresponding Unity `.meta` files alongside any new/modified assets or scripts.
- **Execution:** Task execution must always happen inline in the current session. Do not prompt or ask the user to choose between subagent-driven and inline execution.
- **Changelog:** Add entries under `[Unreleased]` in `CHANGELOG.md`. See `.agent/workflows/changelog.md`
- **Documentation:** After shipping a tool, update `readme.md` and `CHANGELOG.md`. See `.agent/workflows/document-tool.md`
- **Release:** When prompted with `release` (or `release major`, `release minor`, `release patch`), follow `.agent/workflows/release.md`. Check current version in `package.json`, bump version according to SemVer, update `package.json`, stamp `CHANGELOG.md` (convert `## [Unreleased]` to `## [X.Y.Z] - YYYY-MM-DD` and insert a new empty `## [Unreleased]`), verify and update `readme.md` for any new/updated tools or features, and output release summary. Do not auto-commit unless explicitly asked.

