# Git Commit Graph and History Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a modern Git History log and Commit Graph tab to the Git Integration tool in RexTools, featuring dual-column ASCII rendering, clickable SHA copying, and paginated loading.

**Architecture:** Split the main UI into two main tabs (Changes and History) using a double ScrollView. Add an async commit log query method to GitRunner, parse log entries on a tab-separated basis, and render visual columns with monospaced text alignment.

**Tech Stack:** Unity 6 UI Toolkit, Git CLI, C# Tasks

---

### Task 1: Add Git log querying to `GitRunner.cs`

**Files:**
- Modify: [GitRunner.cs](file:///p:/Personal/00%20Unity/03%20RexTools/RexTools/RexTools-Unity6/Editor/Git%20Integration/GitRunner.cs)

- [ ] **Step 1: Implement `GetCommitHistoryAsync`**
  Add the asynchronous git log command method:
  ```csharp
        /// <summary>
        /// Retrieves the commit history log with graph characters and metadata.
        /// </summary>
        public static Task<List<string>> GetCommitHistoryAsync(int skip, int limit)
        {
            var list = new List<string>();
            if (!HasGitRepository()) return Task.FromResult(list);
            
            return RunCommandAsync($"log --graph --pretty=format:\"%h%x09%s%x09%an%x09%ad\" --date=relative --skip={skip} -n {limit}",
                line =>
                {
                    if (!string.IsNullOrWhiteSpace(line))
                    {
                        list.Add(line);
                    }
                }).ContinueWith(t => list);
        }
  ```

- [ ] **Step 2: Commit changes**
  Run:
  ```bash
  git add "Editor/Git Integration/GitRunner.cs"
  git commit -m "feat(git-integration): add GetCommitHistoryAsync method to GitRunner"
  ```

---

### Task 2: Implement UI Styles in USS

**Files:**
- Modify: [GitIntegrationStyles.uss](file:///p:/Personal/00%20Unity/03%20RexTools/RexTools/RexTools-Unity6/Editor/Git%20Integration/GitIntegrationStyles.uss)

- [ ] **Step 1: Add USS style classes for the History columns**
  Append these styles to the stylesheet:
  ```css
  .git-history-header {
      flex-direction: row;
      background-color: rgb(42, 42, 42);
      border-bottom-width: 1px;
      border-bottom-color: rgb(60, 60, 60);
      padding: 6px 8px;
      font-weight: bold;
      color: rgb(150, 150, 150);
  }
  .git-history-header-cell {
      font-size: 11px;
  }
  .git-history-row {
      flex-direction: row;
      align-items: center;
      padding: 4px 8px;
      border-bottom-width: 1px;
      border-bottom-color: rgb(35, 35, 35);
  }
  .git-history-cell-graph {
      font-family: Consolas, Courier, monospace;
      font-size: 11px;
      white-space: pre;
      color: rgb(100, 200, 255);
  }
  .git-history-cell-sha {
      font-family: Consolas, Courier, monospace;
      font-size: 11px;
      color: rgb(255, 193, 7);
      cursor: link;
  }
  .git-history-cell-sha:hover {
      color: rgb(255, 213, 79);
  }
  .git-history-cell-message {
      font-size: 11px;
      color: rgb(220, 220, 220);
      overflow: hidden;
      text-overflow: ellipsis;
      white-space: nowrap;
  }
  .git-history-cell-date {
      font-size: 10px;
      color: rgb(140, 140, 140);
  }
  .git-history-load-more-btn {
      margin: 12px 8px;
      height: 28px;
  }
  .git-history-scroll {
      flex-grow: 1;
      flex-shrink: 1;
      min-height: 150px;
  }
  .git-changes-sub-toggle-container {
      flex-direction: row;
      align-items: center;
      justify-content: flex-end;
      padding: 4px 6px;
      background-color: rgb(35, 35, 35);
      border-bottom-width: 1px;
      border-bottom-color: rgb(50, 50, 50);
  }
  .git-changes-sub-toggle-btn {
      height: 20px;
      font-size: 9px;
      padding-left: 8px;
      padding-right: 8px;
      margin-left: 4px;
      background-color: rgb(45, 45, 45);
      border-width: 1px;
      border-color: rgb(65, 65, 65);
      border-radius: 3px;
  }
  .git-changes-sub-toggle-btn--active {
      background-color: rgb(70, 70, 70);
      color: rgb(255, 255, 255);
  }
  ```

- [ ] **Step 2: Commit changes**
  Run:
  ```bash
  git add "Editor/Git Integration/GitIntegrationStyles.uss"
  git commit -m "feat(git-integration): add USS styles for history view and changes sub-toggle"
  ```

---

### Task 3: Implement Main Tab and Changes Sub-Toggle Layout in Window

**Files:**
- Modify: [GitIntegrationWindow.cs](file:///p:/Personal/00%20Unity/03%20RexTools/RexTools/RexTools-Unity6/Editor/Git%20Integration/GitIntegrationWindow.cs)

- [ ] **Step 1: Declare Layout Fields**
  Declare variables inside `GitIntegrationWindow.cs`:
  ```csharp
          private ScrollView historyScroll;
          private Button loadMoreBtn;
          private Button treeToggleBtn;
          private Button listToggleBtn;
          private int currentHistoryOffset = 0;
          private List<string> historyLines = new List<string>();
          private VisualElement changesSubToggleContainer;
          private VisualElement changesContainer;
          private VisualElement operationsBox;
          private bool isFirstHistoryLoad = true;
          private int currentSubViewIndex = 0; // 0 = Tree, 1 = List
  ```

- [ ] **Step 2: Replace main tab initializer and bind event handler**
  In `CreateGUI()`, change `tabGroup` constructor to initialize `Changes` and `History` tabs:
  ```csharp
              var tabPlaceholder = root.Q<VisualElement>("tab-group-container");
              if (tabPlaceholder != null)
              {
                  tabGroup = new RexTabGroup(new[] { "Changes", "History" });
                  tabGroup.OnTabChanged += SwitchMainTab;
                  tabPlaceholder.Add(tabGroup);
              }
  ```

- [ ] **Step 3: Build sub-toggles and nested ScrollView containers**
  In `CreateGUI()`, initialize the View Mode sub-toggle header and history scroll view:
  ```csharp
              // Build changes sub-toggle container
              changesSubToggleContainer = new VisualElement();
              changesSubToggleContainer.AddToClassList("git-changes-sub-toggle-container");
              
              var toggleLabel = new Label("View Mode:");
              toggleLabel.style.fontSize = 10;
              toggleLabel.style.color = new Color(0.6f, 0.6f, 0.6f);
              changesSubToggleContainer.Add(toggleLabel);

              treeToggleBtn = new Button { text = "Tree" };
              treeToggleBtn.AddToClassList("rex-button");
              treeToggleBtn.AddToClassList("git-changes-sub-toggle-btn");
              treeToggleBtn.clicked += () => SwitchChangesViewMode(0);
              changesSubToggleContainer.Add(treeToggleBtn);

              listToggleBtn = new Button { text = "List" };
              listToggleBtn.AddToClassList("rex-button");
              listToggleBtn.AddToClassList("git-changes-sub-toggle-btn");
              listToggleBtn.clicked += () => SwitchChangesViewMode(1);
              changesSubToggleContainer.Add(listToggleBtn);

              // Find main elements for containment
              changesContainer = new VisualElement();
              changesContainer.style.flexGrow = 1;

              var originalScroll = root.Q<ScrollView>("main-scroll-view");
              if (originalScroll != null)
              {
                  originalScroll.parent.Insert(originalScroll.parent.IndexOf(originalScroll), changesContainer);
                  changesContainer.Add(changesSubToggleContainer);
                  changesContainer.Add(originalScroll);
              }

              operationsBox = root.Q<VisualElement>("main-content-container").Q<VisualElement>(null, "git-ops-box");

              // Build History Scroll Container
              historyScroll = new ScrollView(ScrollViewMode.Vertical);
              historyScroll.AddToClassList("git-history-scroll");
              historyScroll.style.display = DisplayStyle.None;

              var mainContent = root.Q<VisualElement>("main-content-container");
              if (operationsBox != null)
              {
                  operationsBox.parent.Insert(operationsBox.parent.IndexOf(operationsBox), historyScroll);
              }
              else
              {
                  mainContent.Add(historyScroll);
              }
  ```

- [ ] **Step 4: Commit changes**
  Run:
  ```bash
  git add "Editor/Git Integration/GitIntegrationWindow.cs"
  git commit -m "feat(git-integration): add main changes/history tab structure and changes sub-toggle"
  ```

---

### Task 4: Implement Commit Graph Parsing and Rendering in Window

**Files:**
- Modify: [GitIntegrationWindow.cs](file:///p:/Personal/00%20Unity/03%20RexTools/RexTools/RexTools-Unity6/Editor/Git%20Integration/GitIntegrationWindow.cs)

- [ ] **Step 1: Implement `SwitchMainTab` and `SwitchChangesViewMode`**
  Add tab switching logic:
  ```csharp
          private void SwitchMainTab(int index)
          {
              currentTabIndex = index;
              if (changesContainer != null)
              {
                  changesContainer.style.display = index == 0 ? DisplayStyle.Flex : DisplayStyle.None;
              }
              if (historyScroll != null)
              {
                  historyScroll.style.display = index == 1 ? DisplayStyle.Flex : DisplayStyle.None;
              }
              if (operationsBox != null)
              {
                  operationsBox.style.display = index == 0 ? DisplayStyle.Flex : DisplayStyle.None;
              }

              if (index == 0)
              {
                  SwitchChangesViewMode(currentSubViewIndex);
              }
              else if (index == 1)
              {
                  if (isFirstHistoryLoad)
                  {
                      isFirstHistoryLoad = false;
                      _ = RefreshHistoryAsync(true);
                  }
              }
          }

          private void SwitchChangesViewMode(int viewMode)
          {
              currentSubViewIndex = viewMode;
              if (treeToggleBtn != null && listToggleBtn != null)
              {
                  if (viewMode == 0)
                  {
                      treeToggleBtn.AddToClassList("git-changes-sub-toggle-btn--active");
                      listToggleBtn.RemoveFromClassList("git-changes-sub-toggle-btn--active");
                  }
                  else
                  {
                      listToggleBtn.AddToClassList("git-changes-sub-toggle-btn--active");
                      treeToggleBtn.RemoveFromClassList("git-changes-sub-toggle-btn--active");
                  }
              }

              if (treeViewScroll != null && listViewScroll != null)
              {
                  treeViewScroll.style.display = viewMode == 0 ? DisplayStyle.Flex : DisplayStyle.None;
                  listViewScroll.style.display = viewMode == 1 ? DisplayStyle.Flex : DisplayStyle.None;
              }
          }
  ```

- [ ] **Step 2: Implement `RefreshHistoryAsync` and `RenderHistoryRows`**
  Add methods to load, parse, and render history commits:
  ```csharp
          private async Task RefreshHistoryAsync(bool resetOffset)
          {
              if (resetOffset)
              {
                  currentHistoryOffset = 0;
                  historyLines.Clear();
                  if (historyScroll != null) historyScroll.Clear();
              }

              if (currentHistoryOffset == 0 && historyScroll != null)
              {
                  var header = new VisualElement();
                  header.AddToClassList("git-history-header");

                  var gCol = new Label("Graph") { style = { width = 80 } };
                  gCol.AddToClassList("git-history-header-cell");
                  header.Add(gCol);

                  var sCol = new Label("Commit") { style = { width = 70 } };
                  sCol.AddToClassList("git-history-header-cell");
                  header.Add(sCol);

                  var mCol = new Label("Message") { style = { flexGrow = 1 } };
                  mCol.AddToClassList("git-history-header-cell");
                  header.Add(mCol);

                  var dCol = new Label("Date") { style = { width = 90 } };
                  dCol.AddToClassList("git-history-header-cell");
                  header.Add(dCol);

                  historyScroll.Add(header);
              }

              var newLines = await GitRunner.GetCommitHistoryAsync(currentHistoryOffset, 50);
              historyLines.AddRange(newLines);
              currentHistoryOffset += newLines.Count;

              RenderHistoryRows(newLines);

              if (newLines.Count == 50)
              {
                  if (loadMoreBtn == null)
                  {
                      loadMoreBtn = new Button(() => _ = RefreshHistoryAsync(false)) { text = "Load More" };
                      loadMoreBtn.AddToClassList("rex-button");
                      loadMoreBtn.AddToClassList("git-history-load-more-btn");
                  }
                  if (historyScroll != null)
                  {
                      historyScroll.Remove(loadMoreBtn);
                      historyScroll.Add(loadMoreBtn);
                  }
              }
              else if (loadMoreBtn != null && historyScroll != null)
              {
                  historyScroll.Remove(loadMoreBtn);
              }
          }

          private void RenderHistoryRows(List<string> lines)
          {
              if (historyScroll == null) return;

              foreach (var line in lines)
              {
                  string[] parts = line.Split('\t');
                  string graphPart = "";
                  string sha = "";
                  string message = "";
                  string date = "";

                  if (parts.Length == 1)
                  {
                      graphPart = parts[0];
                  }
                  else if (parts.Length > 1)
                  {
                      int lastSpace = parts[0].LastIndexOf(' ');
                      graphPart = lastSpace != -1 ? parts[0].Substring(0, lastSpace) : "";
                      sha = lastSpace != -1 ? parts[0].Substring(lastSpace + 1) : parts[0];
                      message = parts[1];
                      if (parts.Length > 3)
                      {
                          date = parts[3];
                      }
                  }

                  var row = new VisualElement();
                  row.AddToClassList("git-history-row");

                  var gLabel = new Label(graphPart) { style = { width = 80 } };
                  gLabel.AddToClassList("git-history-cell-graph");
                  row.Add(gLabel);

                  var sLabel = new Label(sha) { style = { width = 70 } };
                  sLabel.AddToClassList("git-history-cell-sha");
                  if (!string.IsNullOrEmpty(sha))
                  {
                      string currentSha = sha;
                      sLabel.RegisterCallback<ClickEvent>(evt =>
                      {
                          GUIUtility.systemCopyBuffer = currentSha;
                          Log($"Copied commit SHA {currentSha} to clipboard.");
                      });
                  }
                  row.Add(sLabel);

                  var mLabel = new Label(message) { style = { flexGrow = 1 } };
                  mLabel.AddToClassList("git-history-cell-message");
                  row.Add(mLabel);

                  var dLabel = new Label(date) { style = { width = 90 } };
                  dLabel.AddToClassList("git-history-cell-date");
                  row.Add(dLabel);

                  if (loadMoreBtn != null)
                  {
                      historyScroll.Insert(historyScroll.IndexOf(loadMoreBtn), row);
                  }
                  else
                  {
                      historyScroll.Add(row);
                  }
              }
          }
  ```

- [ ] **Step 3: Redirect tab switching triggers**
  In `CreateGUI()`, update line 215 (from `SwitchTab(0);`) to `SwitchMainTab(0);`.
  Also, update the `tabGroup.OnTabChanged += SwitchTab;` line to call `SwitchMainTab`:
  ```csharp
  tabGroup.OnTabChanged += SwitchMainTab;
  ```
  Ensure the old `SwitchTab` and layout-switching code is cleaned up where appropriate.

- [ ] **Step 4: Commit changes**
  Run:
  ```bash
  git add "Editor/Git Integration/GitIntegrationWindow.cs"
  git commit -m "feat(git-integration): implement commit log parsing, UI rendering, and SHA copy-to-clipboard"
  ```
