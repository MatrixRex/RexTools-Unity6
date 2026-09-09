---
description: how to manage the CHANGELOG.md following Keep a Changelog format
---
### Adding a Change
1. Open `CHANGELOG.md`.
2. Locate the `## [Unreleased]` section.
3. Add your entry under the appropriate subsection:
   - `### Added` for new features.
   - `### Changed` for changes in existing functionality.
   - `### Deprecated` for soon-to-be removed features.
   - `### Removed` for now removed features.
   - `### Fixed` for any bug fixes.
   - `### Security` in case of vulnerabilities.
4. If the subsection doesn't exist under `[Unreleased]`, create it.
5. Entries should be at the top of the list within their section.

### Releasing a New Version
Refer to `.agent/workflows/release.md` for the automated release workflow.
1. Determine bump type (`major`, `minor`, `patch`) or read user prompt.
2. Read current version from `package.json` and compute the bumped version according to Semantic Versioning.
3. Update `"version"` in `package.json`.
4. In `CHANGELOG.md`, rename the existing `## [Unreleased]` header to the new version format: `## [X.Y.Z] - YYYY-MM-DD`.
5. Create a new empty `## [Unreleased]` section at the top.
6. Verify and update `readme.md` for any new tools, features, or updated workflows.
7. Automatically stage and commit the release files with message `chore(release): v<NEW_VERSION>`, then output release summary.

