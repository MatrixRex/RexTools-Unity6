---
description: release workflow to bump version (major, minor, or patch), update package.json, CHANGELOG.md, and readme.md
---

# Release Workflow

Follow this workflow whenever the user prompts to release (`release major`, `release minor`, `release patch`, or general `release`).

---

## 1. Determine the Bump Type

Check the user's prompt for the requested Semantic Versioning (SemVer) increment:
- **`major`**: Breaking changes, fundamental redesigns, or backwards-incompatible API changes.
- **`minor`**: New features, new tools, or significant backward-compatible additions (e.g. adding a new tool or major sub-feature).
- **`patch`**: Bug fixes, performance optimizations, minor UI adjustments, or internal refactoring.

> **If bump type is not explicitly specified in prompt:**
> Inspect `## [Unreleased]` in `CHANGELOG.md`:
> - If breaking changes exist -> `major`
> - If new tools or features exist (`### Added`) -> `minor`
> - If only bug fixes/optimizations exist (`### Fixed`, `### Optimized`, `### Changed`) -> `patch`
> If still ambiguous, briefly ask or confirm the intended bump type with the user before applying edits.

---

## 2. Check Current Version & Calculate Bump

1. Open `package.json` and read the current `"version"` string (e.g. `"0.6.0"`).
2. Deconstruct the version into `MAJOR.MINOR.PATCH` components.
3. Compute the new version according to SemVer:
   - **Major bump**: `(MAJOR + 1).0.0` (e.g. `0.6.0` -> `1.0.0`)
   - **Minor bump**: `MAJOR.(MINOR + 1).0` (e.g. `0.6.0` -> `0.7.0`)
   - **Patch bump**: `MAJOR.MINOR.(PATCH + 1)` (e.g. `0.6.0` -> `0.6.1`)

---

## 3. Bump Version in `package.json`

Update the `"version"` field in `package.json` with the newly calculated version:
```json
{
    "name": "com.matrixrex.rextools",
    "displayName": "RexTools",
    "version": "<NEW_VERSION>",
    ...
}
```

---

## 4. Update `CHANGELOG.md`

Follow the Keep a Changelog standard format:

1. Locate the `## [Unreleased]` section at the top of `CHANGELOG.md`.
2. Verify all unreleased changes delivered since the last release are present under their appropriate subheadings:
   - `### Added`
   - `### Changed`
   - `### Deprecated`
   - `### Removed`
   - `### Fixed`
   - `### Security`
   - `### Optimized` (used in this repository for performance gains)
3. Rename the current `## [Unreleased]` header to the new version with today's date in `YYYY-MM-DD` format:
   ```markdown
   ## [<NEW_VERSION>] - YYYY-MM-DD
   ```
4. Insert a new, empty `## [Unreleased]` section directly above the newly stamped version header:
   ```markdown
   ## [Unreleased]


   ## [<NEW_VERSION>] - YYYY-MM-DD
   ```
5. Maintain all existing historical version entries below.

---

## 5. Update and Verify `readme.md`

1. Open `readme.md`.
2. Inspect changes included in the release:
   - For any newly added tools: Ensure a dedicated section exists under `## 🛠️ Tools` with tool name, description, and actionable usage instructions.
   - For modified tools: Update existing tool descriptions or usage instructions if controls, workflows, or behaviors changed.
3. Verify that any version references, installation paths, or package links are accurate.

---

## 6. Execution and Git Commit

1. Execute all steps inline within the session (do not prompt user to switch execution modes).
2. **Auto-commit after version bump**:
   Automatically stage and commit the release files with the conventional commit message:
   ```bash
   git add package.json CHANGELOG.md CHANGELOG.md.meta readme.md
   git commit -m "chore(release): v<NEW_VERSION>"
   ```
   *(Ensure any other modified or newly added release files/`.meta` files are staged).*
3. Report a clear summary back to the user:
   - Previous version vs. New bumped version.
   - Release type (`major`, `minor`, `patch`).
   - Bulleted summary of release highlights.
   - Commit hash and message.
   - Modified files list (`package.json`, `CHANGELOG.md`, `readme.md`, etc.).

