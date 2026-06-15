# Design Spec: Git Integration Tree View and Tabs

This document outlines the design and architecture for adding a tabbed layout to the Git Integration tool in RexTools, featuring a hierarchical folder tree view as the default and the flat list view as an option.

## Goals

1. **Hierarchical Tree View**: Display changed files nested under their respective project folders.
2. **Bulk Directory Operations**: Checking/unchecking a folder node recursively checks/unchecks all descendant folders and files.
3. **Tabbed Navigation**: Allow toggling between the **Tree View** (default) and **List View** (fallback/original layout) using `RexTabGroup`.
4. **Expand/Collapse Controls**: Add buttons to expand all or collapse all folder nodes in the Tree View.
5. **Aesthetics & Performance**: Ensure smooth scrolling, proper Unity Editor integration (pinging assets on click, displaying correct icons), and retention of folder expand/collapse state across refreshes.

## User Interface Design

### Tabs
- A tab bar using `RexTabGroup` with tabs: **Tree View** and **List View**.
- The tab bar is placed below the repository status information box.

### changed Files List
- **Tree View Tab**:
  - Folders are shown with an expand/collapse arrow symbol (`▼` or `▶`), a folder checkbox, a folder icon, and the folder path fragment name.
  - Files under folders are indented. They have their own checkbox, change status prefix (e.g. `[M]`, `[A]`, `[D]`), file-type icon, and filename.
  - Folder headers have **Select All**, **Deselect All**, **Expand All**, and **Collapse All** buttons.
- **List View Tab**:
  - Displays the original flat list of changed files with relative paths from the repository root.
  - Header displays only **Select All** and **Deselect All** buttons.

## Technical Architecture

### 1. Data Model
To represent the folder structure recursively:
```csharp
private class FolderTreeNode
{
    public string name;
    public string fullPath; // relative to repo root, e.g. "Assets/Scripts"
    public bool isFolder;
    public string fileLine; // raw git status porcelain line
    public string cleanPath; // unquoted file path
    public List<FolderTreeNode> children = new List<FolderTreeNode>();
    
    // UI states and element cache
    public VisualElement rowElement;
    public Toggle checkbox;
    public VisualElement contentContainer;
    public Label arrowLabel;
    public bool isExpanded = true;
}
```

### 2. Checkbox State Propagation
- When a folder checkbox is clicked, it updates all descendant checkboxes recursively.
- The parent hierarchy is updated from bottom to top: if all children of a directory are checked, the parent directory is checked; otherwise, it is unchecked.
- The global `deselectedFiles` set is updated to sync with the check states of all file nodes.

### 3. State Preservation
- To prevent folders from resetting their collapse state every time the repository is scanned or a file changes, the window will maintain a `HashSet<string> collapsedFolders` cache.
- When rebuilding the tree view, the folder node's initial `isExpanded` state is determined by checking whether its path is in the `collapsedFolders` set.

## Verification Plan

### Manual Verification
1. Open Git Integration window via `Tools/Rex Tools/Git Integration`.
2. Verify that two tabs are present: **Tree View** and **List View**, with **Tree View** active by default.
3. In **Tree View**:
   - Verify files are grouped under folder hierarchies.
   - Verify clicking foldout arrows expands/collapses directories.
   - Verify checking a directory checkbox checks/unchecks all its children.
   - Verify clicking **Expand All** and **Collapse All** works as expected.
4. Switch to **List View**:
   - Verify files are shown in the original flat list.
   - Verify **Expand All** and **Collapse All** buttons are hidden.
5. Perform standard operations (commit, discard) on selected files from both views and ensure they execute correctly.
