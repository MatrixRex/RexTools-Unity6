# Design Spec: Git Integration Tool for RexTools

A Git integration extension for the Unity Editor (specifically Unity 6) that exposes common Git command operations, displays sync status, and adds a Git branch button/label to the main play control action strip of the editor toolbar.

## Goal

Provide a lightweight, dependency-free Git tool in the Unity Editor to view the current branch, perform standard operations (Commit, Pull, Push, Fetch), and see remote sync status, with an entry point on the main editor toolbar.

## Core System Architecture

### 1. Process Execution Manager (`GitRunner.cs`)
* **Target Detection**: Detects the closest Git repository starting at `Directory.GetCurrentDirectory()` (project root) and walking up to parent directories until a `.git` folder is found.
* **Asynchronous Running**: Uses `System.Diagnostics.Process` in background threads using `System.Threading.Tasks.Task.Run`.
* **Output Streaming**: Redirects stdout/stderr streams to trigger real-time callbacks.
* **UI Dispatching**: Directs string callbacks to update UI Toolkit controls safely on the main thread via `EditorApplication.delayCall`.

### 2. UI Layout (`GitIntegrationWindow.cs`)
* **Header Stack**: "Rex Tools" brand label + "Git Integration" title + Help button.
* **Repository Info Panel**: Displays repository location and current status (Branch name, commits ahead/behind, modified files count).
* **Action Center**:
  * **Fetch / Refresh**: Manually sync with remote.
  * **Pull**: Fast-forward local branch.
  * **Commit Input**: A text area for the commit message with a primary "Commit All" action (runs `git add -A` and `git commit -m "..."`).
  * **Push**: Push committed changes to remote.
* **Console Terminal Log**: Scrollable output window showing real-time logs from git command stdout/stderr. Includes a "Clear Console" button.

### 3. Toolbar Extender (`GitToolbarExtender.cs`)
* **Registration**: Uses `[InitializeOnLoad]` and waits for `EditorApplication.delayCall`.
* **Reflection Hook**:
  * Finds the `UnityEditor.Toolbar` window instance using `Resources.FindObjectsOfTypeAll`.
  * Extracts the internal `m_Root` VisualElement field.
  * Locates the `ToolbarZonePlayMode` container.
* **Toolbar Button**: Appends an active button/label displaying `Git: <branch>` (e.g. `Git: main` or `Git: feature/cool-stuff`).
* **Trigger Window**: Clicking the label opens the main `GitIntegrationWindow`.
* **Updates**: Refreshes on editor focus changes (`EditorApplication.focusChanged`) and after executing any git command.

## Periodic Sync & Remote Checking
* When the main window is open, an `EditorApplication.update` loop runs a periodic check (every 60 seconds).
* Runs `git fetch` to get updates from the remote.
* Runs `git rev-list --left-right --count HEAD...@{u}` to calculate ahead/behind numbers.
* Runs `git status --porcelain` to check if there are local uncommitted changes.
* Graces authentication issues by outputting them to the Console log rather than hanging.

## Verification Plan

### Manual Verification
1. **Repository Auto-Detection**:
   * Open the window in a project inside a Git repo, check that the path is displayed correctly.
   * Open in a non-git project, check that a warning "No Git repository found" is displayed.
2. **Toolbar Integration**:
   * Verify that the Git branch label is shown next to the Play/Pause buttons in the toolbar.
   * Verify that clicking it opens the Git Integration Window.
3. **Branch Switch Update**:
   * Switch branches in an external terminal/VS Code, focus back on Unity, and verify the toolbar label updates.
4. **Operations Validation**:
   * Verify Commit, Push, Pull, and Fetch output printed to the scrollable console window.
