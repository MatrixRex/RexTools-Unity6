---
description: use this workflow when a new tool/feature is completed to ensure documentation and changelog are updated
---
### 1. Update README.md
Add a new section for the tool under the `## 🛠️ Tools` header.
- **Tool Name**: A clear title.
- **Description**: What does it do? (1-2 sentences).
- **Usage**: Short, actionable steps on how to use it in Unity.
- **Screenshot/GIF**: (Optional) Add a placeholder for a visual guide.

### 2. Update CHANGELOG.md
- Use the `/changelog` workflow to add an entry under `## [Unreleased] > ### Added`.

### 3. Generate Commit Message
- Prepare a commit message following the Conventional Commits format: `feat(tool-name): implementation of [tool name]`.

### 4. Verify versioning
- If this is a significant addition, remind the user about incrementing the version in `package.json` according to SemVer rules.
