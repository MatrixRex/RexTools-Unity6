using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using RexTools.Editor.Core;

namespace RexTools.GitIntegration.Editor
{
    public class GitIntegrationWindow : EditorWindow
    {
        private Label branchStatusLabel;
        private Label syncStatusLabel;
        
        private Button fetchBtn;
        private Button pullBtn;
        private Button pushBtn;
        private Button commitBtn;
        private Button discardBtn;
        private TextField commitMsgField;
        private ScrollView treeViewScroll;
        private ScrollView listViewScroll;
        private FolderTreeNode rootTreeNode;
        private readonly Dictionary<string, Toggle> flatFileToggles = new Dictionary<string, Toggle>();
        private Label changedFilesHeaderLabel;
        
        private VisualElement mainContentContainer;
        private VisualElement noRepoContainer;
        
        private bool isExecuting = false;
        private double lastFetchTime = 0.0f;
        private const double FetchInterval = 60.0; // 60 seconds

        private static readonly string[] SpinnerFrames = { "/", "-", "\\", "|" };
        private int spinnerIndex = 0;
        private double lastSpinnerTime = 0.0;

        private HashSet<string> deselectedFiles = new HashSet<string>();
        private List<string> currentChangedFileLines = new List<string>();
        private List<string> rawChangedFileLines = new List<string>();

        private RexTabGroup tabGroup;
        private int currentTabIndex = 0; // 0 = Changes, 1 = History
        private HashSet<string> collapsedFolders = new HashSet<string>();
        private int updatingCheckboxesCount = 0;
        private Button expandAllBtn;
        private Button collapseAllBtn;
        private readonly Dictionary<string, Texture> iconCache = new Dictionary<string, Texture>();
        private bool isRefreshingStatus = false;

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

        [MenuItem("Tools/Rex Tools/Git Integration")]
        public static void ShowWindow()
        {
            var window = GetWindow<GitIntegrationWindow>("Git Integration");
            window.minSize = new Vector2(380, 390);
        }

        private void OnEnable()
        {
            EditorApplication.update += OnEditorUpdate;
            lastFetchTime = EditorApplication.timeSinceStartup;
        }

        private void OnDisable()
        {
            EditorApplication.update -= OnEditorUpdate;
        }

        public void CreateGUI()
        {
            VisualElement root = rootVisualElement;
            root.AddToClassList("rex-root-padding");

            // Load stylesheets
            LoadStyleSheet(root, "Packages/com.matrixrex.rextools/Editor/RexToolsStyles.uss", "Assets/Editor/RexToolsStyles.uss");
            LoadStyleSheet(root, "Packages/com.matrixrex.rextools/Editor/Git Integration/GitIntegrationStyles.uss", "Assets/Editor/Git Integration/GitIntegrationStyles.uss");

            // Load UXML
            VisualTreeAsset uxml = null;
            string[] possibleUxmlPaths = {
                "Packages/com.matrixrex.rextools/Editor/Git Integration/GitIntegrationWindow.uxml",
                "Assets/Editor/Git Integration/GitIntegrationWindow.uxml"
            };
            foreach (var path in possibleUxmlPaths)
            {
                uxml = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(path);
                if (uxml != null) break;
            }

            if (uxml != null)
            {
                uxml.CloneTree(root);
            }
            else
            {
                Debug.LogError("GitIntegrationWindow: Failed to load UXML layout.");
                return;
            }

            // --- BRANDED HEADER & HELP BOX ---
            var helpBox = new RexHelpBox(
                "Fetch: Fetch updates from remote tracking branch.",
                "Pull: Integrates remote branch updates to local.",
                "Commit: Stage modified files and commit changes.",
                "Push: Upload committed changes to remote repository.",
                "Branch Status: Updates automatically when focus returns to Unity."
            );

            var header = new RexHeader("Git Integration", showHelpButton: true);
            bool showHelp = false;
            header.OnHelpClicked += () => {
                showHelp = !showHelp;
                helpBox.ToggleVisibility();
                header.SetHelpButtonActive(showHelp);
            };

            root.Insert(0, header);
            root.Insert(1, helpBox);

            // Query elements
            mainContentContainer = root.Q<VisualElement>("main-content-container");
            noRepoContainer = root.Q<VisualElement>("no-repo-container");
            branchStatusLabel = root.Q<Label>("branch-status-label");
            syncStatusLabel = root.Q<Label>("sync-status-label");

            var tabPlaceholder = root.Q<VisualElement>("tab-group-container");
            if (tabPlaceholder != null)
            {
                tabGroup = new RexTabGroup(new[] { "Changes", "History" });
                tabGroup.OnTabChanged += SwitchMainTab;
                tabPlaceholder.Add(tabGroup);
            }

            fetchBtn = root.Q<Button>("fetch-btn");
            pullBtn = root.Q<Button>("pull-btn");
            pushBtn = root.Q<Button>("push-btn");
            commitBtn = root.Q<Button>("commit-btn");
            discardBtn = root.Q<Button>("discard-btn");
            commitMsgField = root.Q<TextField>("commit-msg-field");

            // Bind button callbacks
            var scanBtn = root.Q<Button>("scan-btn");
            if (scanBtn != null) scanBtn.clicked += RefreshLayout;

            fetchBtn.clicked += async () => await RunFetchAsync(false);
            pullBtn.clicked += async () => await RunPullAsync();
            pushBtn.clicked += async () => await RunPushAsync();
            commitBtn.clicked += async () => await RunCommitAsync();
            discardBtn.clicked += async () => await RunDiscardSelectedAsync();

            // Dynamically build list inside the container placeholder
            var foldoutContainer = root.Q<VisualElement>("changed-files-foldout-container");
            if (foldoutContainer != null)
            {
                var listHeader = new VisualElement();
                listHeader.AddToClassList("git-list-header");

                changedFilesHeaderLabel = new Label("Changed Files");
                changedFilesHeaderLabel.AddToClassList("rex-section-label");
                changedFilesHeaderLabel.AddToClassList("git-list-header-label");
                listHeader.Add(changedFilesHeaderLabel);

                var buttonContainer = new VisualElement();
                buttonContainer.AddToClassList("git-header-btn-row");

                var selectAllBtn = new Button { text = "Select All" };
                selectAllBtn.AddToClassList("rex-button");
                selectAllBtn.AddToClassList("git-header-btn");
                selectAllBtn.AddToClassList("git-header-btn--left");
                selectAllBtn.clicked += SelectAllChangedFiles;

                var deselectAllBtn = new Button { text = "Deselect All" };
                deselectAllBtn.AddToClassList("rex-button");
                deselectAllBtn.AddToClassList("git-header-btn");
                deselectAllBtn.AddToClassList("git-header-btn--left");
                deselectAllBtn.clicked += DeselectAllChangedFiles;

                expandAllBtn = new Button { text = "Expand All" };
                expandAllBtn.AddToClassList("rex-button");
                expandAllBtn.AddToClassList("git-header-btn");
                expandAllBtn.AddToClassList("git-header-btn--left");
                expandAllBtn.clicked += ExpandAllFolders;

                collapseAllBtn = new Button { text = "Collapse All" };
                collapseAllBtn.AddToClassList("rex-button");
                collapseAllBtn.AddToClassList("git-header-btn");
                collapseAllBtn.clicked += CollapseAllFolders;

                buttonContainer.Add(selectAllBtn);
                buttonContainer.Add(deselectAllBtn);
                buttonContainer.Add(expandAllBtn);
                buttonContainer.Add(collapseAllBtn);
                listHeader.Add(buttonContainer);
                foldoutContainer.Add(listHeader);

                treeViewScroll = new ScrollView(ScrollViewMode.Vertical);
                treeViewScroll.AddToClassList("rex-result-list");
                treeViewScroll.AddToClassList("git-changed-files-scroll");

                listViewScroll = new ScrollView(ScrollViewMode.Vertical);
                listViewScroll.AddToClassList("rex-result-list");
                listViewScroll.AddToClassList("git-changed-files-scroll");

                foldoutContainer.Add(treeViewScroll);
                foldoutContainer.Add(listViewScroll);
            }

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

            RefreshLayout();
            SwitchMainTab(0);
        }

        private void LoadStyleSheet(VisualElement root, string packagePath, string assetsPath)
        {
            StyleSheet styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(packagePath);
            if (styleSheet == null)
            {
                styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(assetsPath);
            }
            if (styleSheet != null)
            {
                root.styleSheets.Add(styleSheet);
            }
        }

        private async void RefreshLayout()
        {
            if (GitRunner.HasGitRepository())
            {
                mainContentContainer.RemoveFromClassList("rex-hidden");
                noRepoContainer.AddToClassList("rex-hidden");
                await RefreshStatusAsync();
            }
            else
            {
                mainContentContainer.AddToClassList("rex-hidden");
                noRepoContainer.RemoveFromClassList("rex-hidden");
            }
        }

        private async Task RefreshStatusAsync()
        {
            if (!GitRunner.HasGitRepository() || isExecuting || isRefreshingStatus) return;

            isRefreshingStatus = true;
            if (treeViewScroll != null && listViewScroll != null)
            {
                treeViewScroll.Clear();
                listViewScroll.Clear();

                var loadingLabel = new Label("Refreshing Git status... [/]");
                loadingLabel.AddToClassList("git-row-path");
                treeViewScroll.Add(loadingLabel);

                var loadingLabelList = new Label("Refreshing Git status... [/]");
                loadingLabelList.AddToClassList("git-row-path");
                listViewScroll.Add(loadingLabelList);
            }

            try
            {
                iconCache.Clear();
                AssetDatabase.Refresh();

                string branch = await GitRunner.GetCurrentBranchAsync();
                var (ahead, behind) = await GitRunner.GetSyncCountsAsync();

                branchStatusLabel.text = $"Branch: {branch}";
                
                // Rebuild the changed files list first to get correct count
                if (treeViewScroll != null)
                {
                    rawChangedFileLines = await GitRunner.GetChangedFilesAsync();
                    currentChangedFileLines = GitRunner.FilterAndDeduplicateChangedFiles(rawChangedFileLines);
                    RebuildChangedFilesListUI();
                }

                int modifiedCount = currentChangedFileLines.Count;
                
                string syncText = "";
                if (ahead == 0 && behind == 0)
                    syncText = "Local is up-to-date with remote.";
                else
                    syncText = $"Ahead: {ahead} commits | Behind: {behind} commits.";

                if (modifiedCount > 0)
                    syncText += $" ({modifiedCount} local uncommitted files)";
                else
                    syncText += " (Clean working directory)";

                syncStatusLabel.text = syncText;
                
                // Sync status to playmode toolbar button
                GitToolbarExtender.ForceRefresh();
            }
            finally
            {
                isRefreshingStatus = false;
            }
        }

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

            if (expandAllBtn != null && collapseAllBtn != null)
            {
                expandAllBtn.style.display = viewMode == 0 ? DisplayStyle.Flex : DisplayStyle.None;
                collapseAllBtn.style.display = viewMode == 0 ? DisplayStyle.Flex : DisplayStyle.None;
            }

            if (treeViewScroll != null && listViewScroll != null)
            {
                treeViewScroll.style.display = viewMode == 0 ? DisplayStyle.Flex : DisplayStyle.None;
                listViewScroll.style.display = viewMode == 1 ? DisplayStyle.Flex : DisplayStyle.None;
            }

            SyncCheckboxStates();
        }

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

        private void ExpandAllFolders()
        {
            collapsedFolders.Clear();
            if (rootTreeNode != null)
            {
                SetTreeExpandedRecursive(rootTreeNode, true);
            }
        }

        private void CollapseAllFolders()
        {
            foreach (var fileLine in currentChangedFileLines)
            {
                string cleanPath = GitRunner.GetFilePathFromLine(fileLine);
                var parents = GitRunner.GetParentDirectories(cleanPath);
                foreach (var parent in parents)
                {
                    collapsedFolders.Add(parent);
                }
            }
            if (rootTreeNode != null)
            {
                SetTreeExpandedRecursive(rootTreeNode, false);
            }
        }

        private void SetTreeExpandedRecursive(FolderTreeNode node, bool expanded)
        {
            foreach (var child in node.children)
            {
                if (child.isFolder)
                {
                    if (expanded)
                    {
                        collapsedFolders.Remove(child.fullPath);
                        if (child.arrowLabel != null) child.arrowLabel.text = "▼";
                        if (child.contentContainer != null) child.contentContainer.style.display = DisplayStyle.Flex;
                    }
                    else
                    {
                        collapsedFolders.Add(child.fullPath);
                        if (child.arrowLabel != null) child.arrowLabel.text = "▶";
                        if (child.contentContainer != null) child.contentContainer.style.display = DisplayStyle.None;
                    }
                    SetTreeExpandedRecursive(child, expanded);
                }
            }
        }

        private void SyncCheckboxStates()
        {
            updatingCheckboxesCount++;
            try
            {
                if (currentSubViewIndex == 0) // Tree View
                {
                    if (rootTreeNode != null)
                    {
                        SyncTreeCheckboxesRecursive(rootTreeNode);
                    }
                }
                else // List View
                {
                    foreach (var kvp in flatFileToggles)
                    {
                        if (kvp.Value != null)
                        {
                            kvp.Value.SetValueWithoutNotify(!deselectedFiles.Contains(kvp.Key));
                        }
                    }
                }
            }
            finally
            {
                updatingCheckboxesCount--;
            }
        }

        private bool SyncTreeCheckboxesRecursive(FolderTreeNode node)
        {
            bool allChecked = true;
            foreach (var child in node.children)
            {
                if (child.isFolder)
                {
                    bool childAllChecked = SyncTreeCheckboxesRecursive(child);
                    if (child.checkbox != null)
                    {
                        child.checkbox.SetValueWithoutNotify(childAllChecked);
                    }
                    if (!childAllChecked)
                    {
                        allChecked = false;
                    }
                }
                else
                {
                    bool isChecked = !deselectedFiles.Contains(child.cleanPath);
                    if (child.checkbox != null)
                    {
                        child.checkbox.SetValueWithoutNotify(isChecked);
                    }
                    if (!isChecked)
                    {
                        allChecked = false;
                    }
                }
            }
            return allChecked;
        }

        private void InsertPathIntoTree(FolderTreeNode root, string fileLine)
        {
            string cleanPath = GitRunner.GetFilePathFromLine(fileLine);
            string[] parts = cleanPath.Split('/');
            FolderTreeNode current = root;

            for (int i = 0; i < parts.Length; i++)
            {
                string part = parts[i];
                bool isLast = (i == parts.Length - 1);

                FolderTreeNode child = current.children.Find(c => c.name == part);
                if (child == null)
                {
                    child = new FolderTreeNode
                    {
                        name = part,
                        fullPath = string.Join("/", parts.Take(i + 1)),
                        isFolder = !isLast,
                        fileLine = isLast ? fileLine : null,
                        cleanPath = isLast ? cleanPath : null,
                        parent = current
                    };
                    current.children.Add(child);
                }
                current = child;
            }
        }

        private bool IsAllChildrenChecked(FolderTreeNode node)
        {
            foreach (var child in node.children)
            {
                if (child.isFolder)
                {
                    if (child.checkbox != null && !child.checkbox.value) return false;
                }
                else
                {
                    if (deselectedFiles.Contains(child.cleanPath)) return false;
                }
            }
            return true;
        }

        private void SetCheckboxesRecursive(FolderTreeNode node, bool value)
        {
            updatingCheckboxesCount++;
            try
            {
                foreach (var child in node.children)
                {
                    if (child.checkbox != null) child.checkbox.SetValueWithoutNotify(value);
                    if (child.isFolder)
                    {
                        SetCheckboxesRecursive(child, value);
                    }
                    else
                    {
                        if (value)
                            deselectedFiles.Remove(child.cleanPath);
                        else
                            deselectedFiles.Add(child.cleanPath);
                    }
                }
            }
            finally
            {
                updatingCheckboxesCount--;
            }
        }

        private void UpdateParentCheckboxes(FolderTreeNode node)
        {
            updatingCheckboxesCount++;
            try
            {
                FolderTreeNode p = node.parent;
                while (p != null && p.parent != null) // parent.parent != null skips virtual root
                {
                    if (p.checkbox != null)
                    {
                        p.checkbox.SetValueWithoutNotify(IsAllChildrenChecked(p));
                    }
                    p = p.parent;
                }
            }
            finally
            {
                updatingCheckboxesCount--;
            }
        }

        private void RenderTree(FolderTreeNode node, VisualElement container, int indentLevel)
        {
            foreach (var child in node.children)
            {
                if (child.isFolder)
                {
                    var folderContainer = new VisualElement();

                    var folderRow = new VisualElement();
                    folderRow.AddToClassList("git-folder-row");
                    folderRow.style.paddingLeft = indentLevel * 12;

                    bool isCollapsed = collapsedFolders.Contains(child.fullPath);
                    var arrow = new Label(isCollapsed ? "▶" : "▼");
                    arrow.AddToClassList("git-folder-arrow");
                    arrow.RegisterCallback<ClickEvent>(evt =>
                    {
                        bool collapsed = collapsedFolders.Contains(child.fullPath);
                        if (collapsed)
                        {
                            collapsedFolders.Remove(child.fullPath);
                            arrow.text = "▼";
                            child.contentContainer.style.display = DisplayStyle.Flex;
                        }
                        else
                        {
                            collapsedFolders.Add(child.fullPath);
                            arrow.text = "▶";
                            child.contentContainer.style.display = DisplayStyle.None;
                        }
                    });
                    folderRow.Add(arrow);
                    child.arrowLabel = arrow;

                    var toggle = new Toggle();
                    toggle.AddToClassList("git-row-checkbox");
                    toggle.value = IsAllChildrenChecked(child);
                    toggle.RegisterValueChangedCallback(evt =>
                    {
                        if (updatingCheckboxesCount > 0) return;
                        SetCheckboxesRecursive(child, evt.newValue);
                        UpdateParentCheckboxes(child);
                        UpdateCommitButtonState();
                    });
                    folderRow.Add(toggle);
                    child.checkbox = toggle;

                    var folderIcon = new Image();
                    folderIcon.image = EditorGUIUtility.IconContent("Folder Icon")?.image;
                    folderIcon.AddToClassList("git-row-icon");
                    folderRow.Add(folderIcon);

                    var nameLabel = new Label(child.name);
                    nameLabel.AddToClassList("rex-section-label");
                    nameLabel.style.marginBottom = 0;
                    nameLabel.style.marginLeft = 4;
                    folderRow.Add(nameLabel);

                    folderContainer.Add(folderRow);

                    var childContainer = new VisualElement();
                    childContainer.style.display = isCollapsed ? DisplayStyle.None : DisplayStyle.Flex;
                    folderContainer.Add(childContainer);
                    child.contentContainer = childContainer;

                    RenderTree(child, childContainer, indentLevel + 1);
                    container.Add(folderContainer);
                }
                else
                {
                    var row = new VisualElement();
                    row.AddToClassList("rex-result-item");
                    row.style.flexDirection = FlexDirection.Row;
                    row.style.alignItems = Align.Center;
                    row.style.paddingLeft = (indentLevel * 12) + 12;

                    var toggle = new Toggle();
                    toggle.value = !deselectedFiles.Contains(child.cleanPath);
                    toggle.AddToClassList("git-row-checkbox");
                    toggle.RegisterValueChangedCallback(evt =>
                    {
                        if (updatingCheckboxesCount > 0) return;
                        if (evt.newValue)
                            deselectedFiles.Remove(child.cleanPath);
                        else
                            deselectedFiles.Add(child.cleanPath);

                        UpdateParentCheckboxes(child);
                        UpdateCommitButtonState();
                    });
                    row.Add(toggle);
                    child.checkbox = toggle;

                    string prefix = child.fileLine.Substring(0, 2);
                    var prefixLabel = new Label($"[{prefix.Trim()}]");
                    prefixLabel.AddToClassList("git-row-prefix");

                    if (prefix.Contains("M"))
                        prefixLabel.AddToClassList("git-row-prefix--modified");
                    else if (prefix.Contains("D"))
                        prefixLabel.AddToClassList("git-row-prefix--deleted");
                    else if (prefix.Contains("A") || prefix.Contains("?"))
                        prefixLabel.AddToClassList("git-row-prefix--added");
                    else
                        prefixLabel.AddToClassList("git-row-prefix--fallback");

                    row.Add(prefixLabel);

                    Texture iconTexture = GetAssetIcon(child.cleanPath);
                    if (iconTexture != null)
                    {
                        var assetIcon = new Image();
                        assetIcon.image = iconTexture;
                        assetIcon.AddToClassList("git-row-icon");
                        row.Add(assetIcon);
                    }

                    var pathLabel = new Label(child.name);
                    pathLabel.AddToClassList("rex-result-name-btn");
                    pathLabel.AddToClassList("git-row-path");

                    pathLabel.RegisterCallback<ClickEvent>(e =>
                    {
                        var asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(child.cleanPath);
                        if (asset != null)
                        {
                            Selection.activeObject = asset;
                            EditorGUIUtility.PingObject(asset);
                        }
                    });
                    row.Add(pathLabel);

                    container.Add(row);
                }
            }
        }

        private void RebuildChangedFilesListUI()
        {
            if (treeViewScroll == null || listViewScroll == null) return;
            
            treeViewScroll.Clear();
            listViewScroll.Clear();
            flatFileToggles.Clear();
            if (changedFilesHeaderLabel != null)
            {
                changedFilesHeaderLabel.text = $"Changed Files ({currentChangedFileLines.Count})";
            }

            if (currentChangedFileLines.Count == 0)
            {
                var cleanLabel = new Label("No changed files (Working directory clean)");
                cleanLabel.AddToClassList("git-row-path");
                treeViewScroll.Add(cleanLabel);
                
                var cleanLabelList = new Label("No changed files (Working directory clean)");
                cleanLabelList.AddToClassList("git-row-path");
                listViewScroll.Add(cleanLabelList);
                
                rootTreeNode = null;
                UpdateCommitButtonState();
                return;
            }

            // 1. Build and render Hierarchical Tree (into treeViewScroll)
            rootTreeNode = new FolderTreeNode { name = "Root", isFolder = true };
            foreach (var fileLine in currentChangedFileLines)
            {
                InsertPathIntoTree(rootTreeNode, fileLine);
            }
            RenderTree(rootTreeNode, treeViewScroll, 0);

            // 2. Build and render Flat List (into listViewScroll)
            foreach (var fileLine in currentChangedFileLines)
            {
                if (fileLine.Length < 3) continue;

                string prefix = fileLine.Substring(0, 2);
                string path = fileLine.Substring(2).Trim();
                string cleanPath = GitRunner.GetFilePathFromLine(fileLine);

                var row = new VisualElement();
                row.AddToClassList("rex-result-item");
                row.style.flexDirection = FlexDirection.Row;
                row.style.alignItems = Align.Center;

                // Checkbox (Tick)
                var toggle = new Toggle();
                toggle.AddToClassList("git-row-checkbox");
                toggle.RegisterValueChangedCallback(evt =>
                {
                    if (updatingCheckboxesCount > 0) return;
                    if (evt.newValue)
                    {
                        deselectedFiles.Remove(cleanPath);
                    }
                    else
                    {
                        deselectedFiles.Add(cleanPath);
                    }
                    UpdateCommitButtonState();
                });
                row.Add(toggle);
                flatFileToggles[cleanPath] = toggle;

                // Prefix Label
                var prefixLabel = new Label($"[{prefix.Trim()}]");
                prefixLabel.AddToClassList("git-row-prefix");

                if (prefix.Contains("M"))
                    prefixLabel.AddToClassList("git-row-prefix--modified");
                else if (prefix.Contains("D"))
                    prefixLabel.AddToClassList("git-row-prefix--deleted");
                else if (prefix.Contains("A") || prefix.Contains("?"))
                    prefixLabel.AddToClassList("git-row-prefix--added");
                else
                    prefixLabel.AddToClassList("git-row-prefix--fallback");

                row.Add(prefixLabel);

                // Asset Icon
                Texture iconTexture = GetAssetIcon(cleanPath);
                if (iconTexture != null)
                {
                    var assetIcon = new Image();
                    assetIcon.image = iconTexture;
                    assetIcon.AddToClassList("git-row-icon");
                    row.Add(assetIcon);
                }

                // File path label
                var pathLabel = new Label(path);
                pathLabel.AddToClassList("rex-result-name-btn");
                pathLabel.AddToClassList("git-row-path");

                pathLabel.RegisterCallback<ClickEvent>(e =>
                {
                    var asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(cleanPath);
                    if (asset != null)
                    {
                        Selection.activeObject = asset;
                        EditorGUIUtility.PingObject(asset);
                    }
                });
                row.Add(pathLabel);

                listViewScroll.Add(row);
            }

            // Sync visual states to current selection
            SyncCheckboxStates();

            // Setup active view visibility
            treeViewScroll.style.display = currentSubViewIndex == 0 ? DisplayStyle.Flex : DisplayStyle.None;
            listViewScroll.style.display = currentSubViewIndex == 1 ? DisplayStyle.Flex : DisplayStyle.None;

            UpdateCommitButtonState();
        }

        private void SelectAllChangedFiles()
        {
            deselectedFiles.Clear();
            SyncCheckboxStates();
            UpdateCommitButtonState();
        }

        private void DeselectAllChangedFiles()
        {
            foreach (var fileLine in currentChangedFileLines)
            {
                string cleanPath = GitRunner.GetFilePathFromLine(fileLine);
                if (!string.IsNullOrEmpty(cleanPath))
                {
                    deselectedFiles.Add(cleanPath);
                }
            }
            SyncCheckboxStates();
            UpdateCommitButtonState();
        }

        private Texture GetAssetIcon(string cleanPath)
        {
            if (iconCache.TryGetValue(cleanPath, out Texture cachedIcon))
            {
                return cachedIcon;
            }

            Texture resolvedIcon = GetAssetIconDirect(cleanPath);
            iconCache[cleanPath] = resolvedIcon;
            return resolvedIcon;
        }

        private Texture GetAssetIconDirect(string cleanPath)
        {
            // 1. Explicitly check if it's a folder/directory first
            string repoRoot = GitRunner.FindRepositoryRoot();
            string fullPath = Path.Combine(repoRoot, cleanPath).Replace("\\", "/");
            if (Directory.Exists(fullPath) || string.IsNullOrEmpty(Path.GetExtension(cleanPath)))
            {
                return EditorGUIUtility.IconContent("Folder Icon")?.image;
            }

            // 2. Try to get the main asset type if the file exists on disk
            Type assetType = AssetDatabase.GetMainAssetTypeAtPath(cleanPath);
            if (assetType != null)
            {
                var content = EditorGUIUtility.ObjectContent(null, assetType);
                if (content != null && content.image != null)
                {
                    return content.image;
                }
            }

            // 3. Fallback based on extension (for deleted files or files not imported yet)
            string ext = Path.GetExtension(cleanPath).ToLowerInvariant();
            switch (ext)
            {
                case ".cs":
                    return EditorGUIUtility.ObjectContent(null, typeof(MonoScript))?.image;
                case ".prefab":
                    return EditorGUIUtility.IconContent("Prefab Icon")?.image ?? EditorGUIUtility.ObjectContent(null, typeof(GameObject))?.image;
                case ".unity":
                    return EditorGUIUtility.IconContent("SceneAsset Icon")?.image ?? EditorGUIUtility.ObjectContent(null, typeof(SceneAsset))?.image;
                case ".mat":
                    return EditorGUIUtility.ObjectContent(null, typeof(Material))?.image;
                case ".png":
                case ".jpg":
                case ".jpeg":
                case ".tga":
                case ".psd":
                case ".tif":
                case ".tiff":
                case ".bmp":
                case ".exr":
                    return EditorGUIUtility.ObjectContent(null, typeof(Texture2D))?.image;
                case ".mp3":
                case ".wav":
                case ".ogg":
                case ".aif":
                case ".aiff":
                    return EditorGUIUtility.ObjectContent(null, typeof(AudioClip))?.image;
                case ".txt":
                case ".json":
                case ".xml":
                case ".csv":
                case ".yaml":
                case ".md":
                case ".uss":
                case ".uxml":
                    return EditorGUIUtility.ObjectContent(null, typeof(TextAsset))?.image;
                case ".shader":
                case ".shaderview":
                case ".shadergraph":
                case ".subgraph":
                    return EditorGUIUtility.IconContent("Shader Icon")?.image ?? EditorGUIUtility.ObjectContent(null, typeof(Shader))?.image;
                case ".anim":
                    return EditorGUIUtility.ObjectContent(null, typeof(AnimationClip))?.image;
                case ".controller":
                    return EditorGUIUtility.ObjectContent(null, typeof(RuntimeAnimatorController))?.image;
                case ".fbx":
                case ".obj":
                case ".blend":
                    return EditorGUIUtility.ObjectContent(null, typeof(Mesh))?.image;
                default:
                    return EditorGUIUtility.IconContent("DefaultAsset Icon")?.image;
            }
        }

        private async Task RunDiscardSelectedAsync()
        {
            var selectedCleanPaths = new List<string>();
            foreach (var fileLine in currentChangedFileLines)
            {
                string cleanPath = GitRunner.GetFilePathFromLine(fileLine);
                if (!deselectedFiles.Contains(cleanPath))
                {
                    selectedCleanPaths.Add(cleanPath);
                }
            }

            if (selectedCleanPaths.Count == 0)
            {
                Log("No files selected to discard.");
                return;
            }

            string fileListStr = string.Join("\n", selectedCleanPaths);
            if (EditorUtility.DisplayDialog("Discard Selected Changes", 
                $"Are you sure you want to discard changes for the {selectedCleanPaths.Count} selected file(s)?\n\n{fileListStr}\n\nThis action cannot be undone.", 
                "Yes", "No"))
            {
                SetUIExecuting(true);
                Log("> Discarding selected files...");
                await GitRunner.DiscardChangesAsync(selectedCleanPaths, rawChangedFileLines);
                Log("Discard completed.");
                SetUIExecuting(false);
                await RefreshStatusAsync();
            }
        }

        private void OnEditorUpdate()
        {
            if (!GitRunner.HasGitRepository()) return;

            double currentTime = EditorApplication.timeSinceStartup;

            if (isExecuting || isRefreshingStatus)
            {
                if (currentTime - lastSpinnerTime > 0.15)
                {
                    lastSpinnerTime = currentTime;
                    spinnerIndex = (spinnerIndex + 1) % SpinnerFrames.Length;
                    string spinner = SpinnerFrames[spinnerIndex];
                    
                    if (isExecuting)
                    {
                        syncStatusLabel.text = $"Executing Git command... [{spinner}]";
                    }
                    else if (isRefreshingStatus)
                    {
                        syncStatusLabel.text = $"Refreshing Git status... [{spinner}]";
                        
                        // Update the text of the loading label in the scroll views
                        if (treeViewScroll != null && treeViewScroll.childCount > 0)
                        {
                            var label = treeViewScroll[0] as Label;
                            if (label != null && label.text.StartsWith("Refreshing Git status..."))
                            {
                                label.text = $"Refreshing Git status... [{spinner}]";
                            }
                        }
                        if (listViewScroll != null && listViewScroll.childCount > 0)
                        {
                            var label = listViewScroll[0] as Label;
                            if (label != null && label.text.StartsWith("Refreshing Git status..."))
                            {
                                label.text = $"Refreshing Git status... [{spinner}]";
                            }
                        }
                    }
                }
                return;
            }

            if (currentTime - lastFetchTime > FetchInterval)
            {
                lastFetchTime = currentTime;
                _ = RunFetchAsync(true); // run silent fetch in background
            }
        }

        private void SetUIExecuting(bool executing)
        {
            isExecuting = executing;
            fetchBtn.SetEnabled(!executing);
            pullBtn.SetEnabled(!executing);
            pushBtn.SetEnabled(!executing);
            commitMsgField.SetEnabled(!executing);

            if (executing)
            {
                commitBtn.SetEnabled(false);
                discardBtn.SetEnabled(false);
            }
            else
            {
                UpdateCommitButtonState();
            }

            // Set the global network flag on GitRunner
            GitRunner.IsRunningNetworkCommand = executing;
        }

        private void UpdateCommitButtonState()
        {
            if (isExecuting) return;

            bool hasSelected = false;
            foreach (var fileLine in currentChangedFileLines)
            {
                string cleanPath = GitRunner.GetFilePathFromLine(fileLine);
                if (!deselectedFiles.Contains(cleanPath))
                {
                    hasSelected = true;
                    break;
                }
            }

            commitBtn.SetEnabled(hasSelected);
            discardBtn.SetEnabled(hasSelected);
        }

        private void Log(string text)
        {
            if (string.IsNullOrEmpty(text)) return;
            Debug.Log($"[Git] {text}");
        }

        private void LogError(string text)
        {
            if (string.IsNullOrEmpty(text)) return;
            Debug.LogError($"[Git] {text}");
        }

        private async Task RunFetchAsync(bool silent)
        {
            if (isExecuting) return;
            if (!silent)
            {
                SetUIExecuting(true);
                Log("> git fetch");
            }

            var outputLines = new List<string>();
            int exitCode = await GitRunner.RunCommandAsync("fetch", 
                line => { if (!silent) { Log(line); outputLines.Add(line); } }, 
                line => { if (!silent) { Log(line); outputLines.Add(line); } }
            );

            if (!silent)
            {
                Log($"Fetch finished with exit code {exitCode}");
                SetUIExecuting(false);

                if (exitCode != 0)
                {
                    string errorSummary = outputLines.Count > 0 ? string.Join("\n", outputLines.TakeLast(5)) : "Unknown error.";
                    EditorUtility.DisplayDialog("Git Fetch Failed", 
                        $"Failed to fetch updates from remote repository (Exit Code: {exitCode}).\n\nDetails:\n{errorSummary}", 
                        "OK");
                }
                else
                {
                    EditorUtility.DisplayDialog("Git Fetch Succeeded", 
                        "Successfully fetched updates from remote tracking branches.", 
                        "OK");
                }
            }
            await RefreshStatusAsync();
        }

        private async Task RunPullAsync()
        {
            if (isExecuting) return;
            SetUIExecuting(true);
            Log("> git pull");

            var outputLines = new List<string>();
            int exitCode = await GitRunner.RunCommandAsync("pull", 
                line => { Log(line); outputLines.Add(line); }, 
                line => { Log(line); outputLines.Add(line); }
            );
            Log($"Pull finished with exit code {exitCode}");
            
            SetUIExecuting(false);
            await RefreshStatusAsync();

            if (exitCode != 0)
            {
                string errorSummary = outputLines.Count > 0 ? string.Join("\n", outputLines.TakeLast(5)) : "Unknown error.";
                EditorUtility.DisplayDialog("Git Pull Failed", 
                    $"Failed to pull changes from remote repository (Exit Code: {exitCode}).\n\nDetails:\n{errorSummary}", 
                    "OK");
            }
            else
            {
                EditorUtility.DisplayDialog("Git Pull Succeeded", 
                    "Successfully pulled and merged changes from remote repository.", 
                    "OK");
            }
        }

        private async Task RunPushAsync()
        {
            if (isExecuting) return;
            SetUIExecuting(true);
            Log("> git push");

            var outputLines = new List<string>();
            int exitCode = await GitRunner.RunCommandAsync("push", 
                line => { Log(line); outputLines.Add(line); }, 
                line => { Log(line); outputLines.Add(line); }
            );
            Log($"Push finished with exit code {exitCode}");
            
            SetUIExecuting(false);
            await RefreshStatusAsync();

            if (exitCode != 0)
            {
                string errorSummary = outputLines.Count > 0 ? string.Join("\n", outputLines.TakeLast(5)) : "Unknown error.";
                EditorUtility.DisplayDialog("Git Push Failed", 
                    $"Failed to push changes to remote repository (Exit Code: {exitCode}).\n\nDetails:\n{errorSummary}", 
                    "OK");
            }
            else
            {
                EditorUtility.DisplayDialog("Git Push Succeeded", 
                    "Successfully pushed committed changes to remote repository.", 
                    "OK");
            }
        }

        private async Task RunCommitAsync()
        {
            if (isExecuting) return;

            string message = commitMsgField.value.Trim();
            if (string.IsNullOrEmpty(message))
            {
                Log("Error: Commit message cannot be empty.");
                return;
            }

            var selectedCleanPaths = new List<string>();
            foreach (var fileLine in currentChangedFileLines)
            {
                string cleanPath = GitRunner.GetFilePathFromLine(fileLine);
                if (!deselectedFiles.Contains(cleanPath))
                {
                    selectedCleanPaths.Add(cleanPath);
                }
            }

            if (selectedCleanPaths.Count == 0)
            {
                Log("Error: No files selected to commit.");
                return;
            }

            SetUIExecuting(true);

            var outputLines = new List<string>();
            bool success = await GitRunner.CommitChangesAsync(
                selectedCleanPaths, 
                rawChangedFileLines, 
                message, 
                line => { Log(line); outputLines.Add(line); }, 
                line => { Log(line); outputLines.Add(line); }
            );

            if (success)
            {
                commitMsgField.value = "";
                EditorUtility.DisplayDialog("Commit Succeeded", 
                    "Selected changes have been successfully committed.", 
                    "OK");
            }
            else
            {
                string errorSummary = outputLines.Count > 0 ? string.Join("\n", outputLines.TakeLast(5)) : "Commit process failed.";
                EditorUtility.DisplayDialog("Commit Failed", 
                    $"Failed to commit changes.\n\nDetails:\n{errorSummary}", 
                    "OK");
            }

            SetUIExecuting(false);
            await RefreshStatusAsync();
        }

        private class FolderTreeNode
        {
            public string name;
            public string fullPath;
            public bool isFolder;
            public string fileLine;
            public string cleanPath;
            public List<FolderTreeNode> children = new List<FolderTreeNode>();
            public FolderTreeNode parent;
            
            // Cached UI Elements
            public VisualElement rowElement;
            public Toggle checkbox;
            public VisualElement contentContainer;
            public Label arrowLabel;
        }
    }
}
