# Design Spec: Git Integration Commit Graph and History

This document outlines the design and architecture for adding a Git Commit History view to the Git Integration tool in RexTools, featuring a multi-column layout with an ASCII-based commit graph, clickable copy-to-clipboard SHA codes, and paginated log retrieval.

## Goals

1. **Tab Reorganization**: Split the UI into two main top-level tabs: **Changes** (for staging, committing, and discarding files) and **History** (for viewing commit log).
2. **Sub-Toggle for Changes**: Move the uncommitted file view selection (Tree View / List View) into a sub-tab group or visual toggle inside the **Changes** view.
3. **Commit Graph View**: Retrieve and render the git commit graph (`git log --graph`).
4. **Dual Column Layout**: Render the monospaced ASCII graph lines on the left, and structured commit metadata (SHA, Message, Date) on the right in aligned columns.
5. **Interactive SHA Copy**: Allow users to click on any commit SHA to copy the full commit hash to the system clipboard.
6. **Paginated Loading**: Load commits in batches of 50, with a "Load More" button at the bottom of the log to fetch older history dynamically.
7. **Main Thread Responsiveness**: Run all git commands asynchronously on background threads using `GitRunner`.

## User Interface Design

### 1. Main Navigation Tab Group
* Top-level tabs: **Changes** and **History**.
* Switching to **Changes** shows the uncommitted files, the selection sub-toggle (Tree / List), and the Operations Box.
* Switching to **History** displays the commit log view and hides the Operations Box.

### 2. Changes Sub-Toggle
* A small two-button layout (e.g. "Tree" and "List") in the header bar of the changed files list.

### 3. History View List Layout
* Column headers at the top: **Graph**, **Commit**, **Message**, **Date**.
* Each commit row consists of:
  * **Graph column**: Monospaced font (`Courier` / `consola`), fixed width (`80px`), vertical alignment.
  * **SHA column**: Monospaced font, fixed width (`70px`), gold colored text, hover cursor set to link. Click triggers copying the full SHA to the clipboard.
  * **Message column**: Flexible width (`flex-grow: 1`), normal font, clipping with ellipsis.
  * **Date column**: Fixed width (`90px`), grey text, showing relative times (e.g. `2 hours ago`, `5 days ago`).

## Technical Architecture

### 1. Git History Fetching
We will add `GetCommitHistoryAsync` to `GitRunner.cs`:
```csharp
public static Task<List<string>> GetCommitHistoryAsync(int skip, int limit)
{
    var list = new List<string>();
    // Run git log --graph with tabs separating the metadata fields
    return RunCommandAsync($"log --graph --pretty=format:\"%h%x09%s%x09%an%x09%ad\" --date=relative --skip={skip} -n {limit}",
        line => {
            if (!string.IsNullOrWhiteSpace(line))
            {
                list.Add(line);
            }
        }).ContinueWith(_ => list);
}
```

### 2. Log Line Parsing
In `GitIntegrationWindow.cs`, the log output lines are parsed:
* Split by tab: `string[] parts = line.Split('\t')`.
* If `parts.Length == 1`:
  * A pure graph/merge line with no commit data.
  * `GraphPart = parts[0]`
  * `SHA = ""`, `Message = ""`, `Date = ""`
* If `parts.Length > 1`:
  * A commit entry row.
  * Parse `parts[0]` for the SHA and Graph:
    * `int lastSpace = parts[0].LastIndexOf(' ')`
    * `GraphPart = lastSpace != -1 ? parts[0].Substring(0, lastSpace) : ""`
    * `SHA = lastSpace != -1 ? parts[0].Substring(lastSpace + 1) : parts[0]`
  * `Message = parts[1]`
  * `Author = parts[2]`
  * `Date = parts[3]`

### 3. State Preservation and Pagination
* `private List<string> historyLines = new List<string>();`
* `private int currentHistoryOffset = 0;`
* When switching to the **History** tab:
  * If `historyLines` is empty, trigger the initial load of 50 commits.
  * If already loaded, show the cached entries immediately without querying Git again.
* **Load More**:
  * Click triggers `GetCommitHistoryAsync(currentHistoryOffset, 50)`.
  * The results are parsed and appended to both the list and the UI display.
  * `currentHistoryOffset` is incremented by the number of commits returned.

## Verification Plan

### Manual Verification
1. Open Git Integration window.
2. Verify that two main tabs are present: **Changes** and **History**.
3. In **Changes**:
   * Verify the uncommitted files are displayed.
   * Verify the sub-toggles "Tree" and "List" correctly switch layout of the uncommitted files.
   * Verify Operations Box is visible at the bottom.
4. Click **History**:
   * Verify the Operations Box is hidden.
   * Verify a loading spinner or indicator is displayed, followed by the commit history log.
   * Verify the graph characters are monospaced and align vertically.
   * Verify that clicking on a gold SHA copies the SHA to the clipboard and shows a log confirmation.
   * Scroll to the bottom and click **Load More**. Verify the next 50 commits are fetched and appended seamlessly.
