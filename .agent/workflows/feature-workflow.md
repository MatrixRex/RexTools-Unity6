---
description: workflow for developing new features on temporary branches and requiring explicit user confirmation before merging to main, clearing the temporary branch, or bumping version
---

# Feature Branch & Completion Workflow

Follow this workflow whenever starting, developing, or finishing a new feature, tool, or non-trivial enhancement.

---

## Core Rules & Guardrails

> [!IMPORTANT]
> 1. **Never develop directly on `main`**: All feature work must take place on a dedicated temporary branch.
> 2. **No unconfirmed Git or version actions**: Under NO circumstances should the agent merge, delete branches, commit to `main`, or bump versions without explicit user confirmation.
> 3. **Respect User Decisions**:
>    - If the user agrees to **both** (merge & version bump): execute both.
>    - If the user agrees to **merge only**: merge and clear the temp branch, but do not bump version.
>    - If the user agrees to **version bump only**: bump version without merging to `main`.
>    - If the user agrees to **none / declines**: do nothing (keep the branch intact, do not merge or bump).
> 4. **Unity `.meta` files**: Always stage and commit `.meta` files together with their corresponding assets or code.

---

## 1. Starting a Feature / Task

Before writing or editing code for a new feature:

1. Check the current branch:
   ```bash
   git branch --show-current
   ```
2. If currently on `main` (or another shared/release branch):
   - Determine a temporary branch name based on the feature (e.g., `feature/<feature-name>` or `temp/<feature-name>`).
   - Create and check out the new temporary branch:
     ```bash
     git checkout -b feature/<feature-name>
     ```
3. Confirm that `git branch --show-current` reflects the temporary branch.
4. Perform all feature coding, file creation, and local validation on this temporary branch.

---

## 2. During Development

- Keep all changes isolated to the temporary branch.
- Document added or changed functionality under `## [Unreleased]` in `CHANGELOG.md` as work progresses (see `.agent/workflows/changelog.md`).
- Do not push or merge into `main` during active development.

---

## 3. Completion Trigger & Confirmation Step

When the user indicates that feature work is done (e.g., *"work is done"*, *"finished"*, *"looks good"*, *"ready to merge"*):

### 🛑 STOP: Do NOT Automatically Commit, Merge, or Bump Version!

Ask the user explicitly for confirmation with clear choices:

> **Example Agent Prompt:**
> "The feature work for `<feature-name>` is complete on branch `<temp-branch>`.
> Would you like to:
> 1. **Both**: Merge into `main`, clear (delete) the temporary branch, and bump the version?
> 2. **Merge Only**: Merge into `main` and clear (delete) the temporary branch without bumping version?
> 3. **Version Bump Only**: Bump version without merging to `main`?
> 4. **None**: Keep everything on the temporary branch as-is (no merge, no version bump)?"

Wait for the user's explicit response before taking any action.

---

## 4. Executing Based on User Choice

### Case A: User Agrees to Both (Merge & Version Bump)

1. **Commit changes on the temporary branch** (ensure `.meta` files are staged):
   ```bash
   git add -A
   git commit -m "feat(<tool-name>): <description>"
   ```
2. **Switch to `main`**:
   ```bash
   git checkout main
   ```
3. **Merge the temporary feature branch**:
   ```bash
   git merge feature/<feature-name>
   ```
4. **Clear (delete) the temporary branch**:
   ```bash
   git branch -d feature/<feature-name>
   ```
5. **Execute Version Bump**:
   - Follow [.agent/workflows/release.md](file:///e:/Nazmul/02%20Personal/Unity6%20Rex%20Tools/RexTools-Unity6/.agent/workflows/release.md).
   - Determine bump level (`major`, `minor`, or `patch`) based on changes or user instruction.
   - Update `package.json`, stamp `CHANGELOG.md`, and verify `readme.md`.
   - Commit the release:
     ```bash
     git add package.json CHANGELOG.md readme.md
     git commit -m "chore(release): v<NEW_VERSION>"
     ```
6. Report completion summary to the user.

---

### Case B: User Agrees to Merge Only (No Version Bump)

1. **Commit changes on the temporary branch** (ensure `.meta` files are staged):
   ```bash
   git add -A
   git commit -m "feat(<tool-name>): <description>"
   ```
2. **Switch to `main`**:
   ```bash
   git checkout main
   ```
3. **Merge the temporary feature branch**:
   ```bash
   git merge feature/<feature-name>
   ```
4. **Clear (delete) the temporary branch**:
   ```bash
   git branch -d feature/<feature-name>
   ```
5. Report completion summary (merged into `main` and temp branch deleted; version unchanged).

---

### Case C: User Agrees to Version Bump Only (No Merge)

1. Stay on the temporary branch (do not switch to `main`, do not merge).
2. Follow [.agent/workflows/release.md](file:///e:/Nazmul/02%20Personal/Unity6%20Rex%20Tools/RexTools-Unity6/.agent/workflows/release.md) to update `package.json`, `CHANGELOG.md`, and `readme.md`.
3. Stage and commit if requested by the user.
4. Report completion summary.

---

### Case D: User Agrees to None (or Declines)

1. **Do not switch to `main`**.
2. **Do not merge**.
3. **Do not delete the temporary branch**.
4. **Do not bump version**.
5. Inform the user that the branch and uncommitted/committed changes remain untouched on `feature/<feature-name>`.

---

## 5. Safety Checklist

- [ ] Current branch verified before writing feature code (not `main`).
- [ ] Explicit user confirmation received before any merge or version bump.
- [ ] All new/modified Unity assets and scripts have accompanying `.meta` files staged.
- [ ] No uncommitted files accidentally lost when switching branches.
- [ ] If merge conflicts occur, immediately halt and prompt the user—never force-merge or discard work.

