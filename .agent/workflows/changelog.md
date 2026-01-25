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
1. Determine the new version number (e.g., `0.0.2`) based on Semantic Versioning.
2. Update the version in `package.json`.
3. In `CHANGELOG.md`, rename the existing `## [Unreleased]` header to the new version format: `## [0.0.2] - 2026-01-25`.
4. Create a new empty `## [Unreleased]` section at the top.
5. (Optional) Update the comparison links at the footer of the document.
