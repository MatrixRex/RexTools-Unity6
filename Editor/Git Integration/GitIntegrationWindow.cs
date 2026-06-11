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
        private Label repoPathLabel;
        private Label branchStatusLabel;
        private Label syncStatusLabel;
        
        private Button fetchBtn;
        private Button pullBtn;
        private Button pushBtn;
        private Button commitBtn;
        private Button discardBtn;
        private TextField commitMsgField;
        private ScrollView changedFilesScroll;
        private RexFoldout changedFilesFoldout;
        
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

            // Load Global Styles
            string[] possiblePaths = {
                "Packages/com.matrixrex.rextools/Editor/RexToolsStyles.uss",
                "Assets/Editor/RexToolsStyles.uss"
            };
            StyleSheet styleSheet = null;
            foreach (var path in possiblePaths)
            {
                styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(path);
                if (styleSheet != null) break;
            }
            if (styleSheet != null) root.styleSheets.Add(styleSheet);

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

            root.Add(header);
            root.Add(helpBox);

            // --- CONTAINER SWITCHERS ---
            mainContentContainer = new VisualElement();
            noRepoContainer = new VisualElement();

            root.Add(mainContentContainer);
            root.Add(noRepoContainer);

            BuildMainLayout();
            BuildNoRepoLayout();

            // Initial check
            RefreshLayout();
        }

        private void BuildNoRepoLayout()
        {
            noRepoContainer.AddToClassList("rex-box");
            noRepoContainer.style.alignItems = Align.Center;
            noRepoContainer.style.paddingTop = 20;
            noRepoContainer.style.paddingBottom = 20;
            noRepoContainer.style.paddingLeft = 20;
            noRepoContainer.style.paddingRight = 20;

            var warningLabel = new Label("No Git repository found in the project root or parent directories.");
            warningLabel.style.whiteSpace = WhiteSpace.Normal;
            warningLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
            warningLabel.style.marginBottom = 15;
            noRepoContainer.Add(warningLabel);

            var detectBtn = new Button { text = "Scan for Repository" };
            detectBtn.AddToClassList("rex-action-button");
            detectBtn.AddToClassList("rex-action-button--pack");
            detectBtn.style.width = 200;
            detectBtn.style.height = 30;
            detectBtn.clicked += RefreshLayout;
            noRepoContainer.Add(detectBtn);
        }

        private void BuildMainLayout()
        {
            // --- REPOSITORY INFO ---
            var infoBox = new VisualElement();
            infoBox.AddToClassList("rex-box");

            var infoLabel = new Label("REPOSITORY STATUS");
            infoLabel.AddToClassList("rex-section-label");
            infoBox.Add(infoLabel);

            repoPathLabel = new Label("Path: --");
            repoPathLabel.style.fontSize = 10;
            repoPathLabel.style.color = new Color(0.7f, 0.7f, 0.7f);
            infoBox.Add(repoPathLabel);

            branchStatusLabel = new Label("Branch: --");
            branchStatusLabel.style.fontSize = 12;
            branchStatusLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            infoBox.Add(branchStatusLabel);

            syncStatusLabel = new Label("Sync: --");
            syncStatusLabel.style.fontSize = 11;
            infoBox.Add(syncStatusLabel);

            mainContentContainer.Add(infoBox);

            // --- CHANGED FILES LIST (RexFoldout + RexList) ---
            changedFilesFoldout = new RexFoldout("Changed Files", count: 0, defaultExpanded: true);
            changedFilesFoldout.style.marginTop = 6;

            var listHeader = new VisualElement();
            listHeader.style.flexDirection = FlexDirection.Row;
            listHeader.style.justifyContent = Justify.SpaceBetween;
            listHeader.style.alignItems = Align.Center;
            listHeader.style.paddingLeft = 6;
            listHeader.style.paddingRight = 6;
            listHeader.style.paddingBottom = 4;
            listHeader.style.borderBottomWidth = 1;
            listHeader.style.borderBottomColor = new Color(0.2f, 0.2f, 0.2f);
            listHeader.style.marginBottom = 4;

            var headerLabel = new Label("Changed Files");
            headerLabel.AddToClassList("rex-section-label");
            headerLabel.style.marginBottom = 0;
            listHeader.Add(headerLabel);

            var buttonContainer = new VisualElement();
            buttonContainer.style.flexDirection = FlexDirection.Row;

            var selectAllBtn = new Button { text = "Select All" };
            selectAllBtn.AddToClassList("rex-button");
            selectAllBtn.style.marginRight = 4;
            selectAllBtn.style.height = 18;
            selectAllBtn.style.fontSize = 9;
            selectAllBtn.style.paddingLeft = 6;
            selectAllBtn.style.paddingRight = 6;
            selectAllBtn.clicked += SelectAllChangedFiles;

            var deselectAllBtn = new Button { text = "Deselect All" };
            deselectAllBtn.AddToClassList("rex-button");
            deselectAllBtn.style.height = 18;
            deselectAllBtn.style.fontSize = 9;
            deselectAllBtn.style.paddingLeft = 6;
            deselectAllBtn.style.paddingRight = 6;
            deselectAllBtn.clicked += DeselectAllChangedFiles;

            buttonContainer.Add(selectAllBtn);
            buttonContainer.Add(deselectAllBtn);
            listHeader.Add(buttonContainer);
            changedFilesFoldout.Add(listHeader);

            changedFilesScroll = new ScrollView(ScrollViewMode.Vertical);
            changedFilesScroll.AddToClassList("rex-result-list");
            changedFilesScroll.style.height = 120;
            
            changedFilesFoldout.Add(changedFilesScroll);
            mainContentContainer.Add(changedFilesFoldout);

            // --- OPERATIONS PANEL ---
            var opsBox = new VisualElement();
            opsBox.AddToClassList("rex-box");

            var opsLabel = new Label("OPERATIONS");
            opsLabel.AddToClassList("rex-section-label");
            opsBox.Add(opsLabel);

            var buttonRow = new VisualElement();
            buttonRow.AddToClassList("rex-row");

            fetchBtn = new Button { text = "Fetch" };
            fetchBtn.style.flexGrow = 1;
            fetchBtn.clicked += async () => await RunFetchAsync(false);
            buttonRow.Add(fetchBtn);

            pullBtn = new Button { text = "Pull" };
            pullBtn.style.flexGrow = 1;
            pullBtn.clicked += async () => await RunPullAsync();
            buttonRow.Add(pullBtn);

            pushBtn = new Button { text = "Push" };
            pushBtn.style.flexGrow = 1;
            pushBtn.clicked += async () => await RunPushAsync();
            buttonRow.Add(pushBtn);

            opsBox.Add(buttonRow);

            // Commit Section
            commitMsgField = new TextField("Commit Message");
            commitMsgField.multiline = true;
            commitMsgField.style.height = 60;
            commitMsgField.style.marginTop = 10;
            opsBox.Add(commitMsgField);

            var commitActionRow = new VisualElement();
            commitActionRow.AddToClassList("rex-row");
            commitActionRow.style.marginTop = 8;

            commitBtn = new Button { text = "COMMIT SELECTED" };
            commitBtn.AddToClassList("rex-action-button");
            commitBtn.AddToClassList("rex-action-button--pack");
            commitBtn.style.flexGrow = 1;
            commitBtn.style.height = 36;
            commitBtn.style.marginTop = 0;
            commitBtn.clicked += async () => await RunCommitAsync();
            commitActionRow.Add(commitBtn);

            discardBtn = new Button { text = "DISCARD SELECTED" };
            discardBtn.AddToClassList("rex-action-button");
            discardBtn.AddToClassList("rex-action-button--pack");
            discardBtn.style.backgroundColor = new Color(0.7f, 0.2f, 0.2f);
            discardBtn.style.flexGrow = 1;
            discardBtn.style.height = 36;
            discardBtn.style.marginTop = 0;
            discardBtn.style.marginLeft = 5;
            discardBtn.clicked += async () => await RunDiscardSelectedAsync();
            commitActionRow.Add(discardBtn);

            opsBox.Add(commitActionRow);

            mainContentContainer.Add(opsBox);
        }

        private async void RefreshLayout()
        {
            if (GitRunner.HasGitRepository())
            {
                mainContentContainer.RemoveFromClassList("rex-hidden");
                noRepoContainer.AddToClassList("rex-hidden");
                repoPathLabel.text = $"Path: {GitRunner.FindRepositoryRoot()}";
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
            if (!GitRunner.HasGitRepository() || isExecuting) return;

            AssetDatabase.Refresh();

            string branch = await GitRunner.GetCurrentBranchAsync();
            var (ahead, behind) = await GitRunner.GetSyncCountsAsync();
            int modifiedCount = await GitRunner.GetModifiedFilesCountAsync();

            branchStatusLabel.text = $"Branch: {branch}";
            
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

            // Rebuild the changed files list
            if (changedFilesScroll != null)
            {
                var changedFiles = await GitRunner.GetChangedFilesAsync();
                currentChangedFileLines = changedFiles;
                RebuildChangedFilesListUI();
            }
            
            // Sync status to playmode toolbar button
            GitToolbarExtender.ForceRefresh();
        }

        private void RebuildChangedFilesListUI()
        {
            if (changedFilesScroll == null) return;
            
            changedFilesScroll.Clear();
            changedFilesFoldout.SetCount(currentChangedFileLines.Count);

            if (currentChangedFileLines.Count == 0)
            {
                var cleanLabel = new Label("No changed files (Working directory clean)");
                cleanLabel.style.color = new Color(0.5f, 0.5f, 0.5f);
                cleanLabel.style.fontSize = 10;
                cleanLabel.style.paddingLeft = 4;
                changedFilesScroll.Add(cleanLabel);
            }
            else
            {
                foreach (var fileLine in currentChangedFileLines)
                {
                    if (fileLine.Length < 3) continue;

                    string prefix = fileLine.Substring(0, 2);
                    string path = fileLine.Substring(2).Trim();
                    string cleanPath = GetFilePathFromLine(fileLine);

                    var row = new VisualElement();
                    row.AddToClassList("rex-result-item");
                    row.style.flexDirection = FlexDirection.Row;
                    row.style.alignItems = Align.Center;

                    // Checkbox (Tick)
                    var toggle = new Toggle();
                    toggle.value = !deselectedFiles.Contains(cleanPath);
                    toggle.style.marginRight = 4;
                    toggle.RegisterValueChangedCallback(evt =>
                    {
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

                    // Prefix Label
                    var prefixLabel = new Label($"[{prefix.Trim()}]");
                    prefixLabel.style.width = 32;
                    prefixLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
                    prefixLabel.style.fontSize = 10;
                    prefixLabel.style.paddingLeft = 4;

                    if (prefix.Contains("M"))
                        prefixLabel.style.color = new Color(1.0f, 0.7f, 0.2f); // Orange/Yellow
                    else if (prefix.Contains("D"))
                        prefixLabel.style.color = new Color(1.0f, 0.35f, 0.35f); // Red
                    else if (prefix.Contains("A") || prefix.Contains("?"))
                        prefixLabel.style.color = new Color(0.3f, 0.8f, 0.4f); // Green
                    else
                        prefixLabel.style.color = new Color(0.8f, 0.8f, 0.8f);

                    row.Add(prefixLabel);

                    // File path label
                    var pathLabel = new Label(path);
                    pathLabel.AddToClassList("rex-result-name-btn");
                    pathLabel.style.flexGrow = 1;
                    pathLabel.style.fontSize = 10;
                    pathLabel.style.color = new Color(0.85f, 0.85f, 0.85f);
                    pathLabel.style.paddingLeft = 4;

                    // Hover effect
                    pathLabel.RegisterCallback<MouseOverEvent>(e => pathLabel.style.color = new Color(0.4f, 0.8f, 1.0f));
                    pathLabel.RegisterCallback<MouseOutEvent>(e => pathLabel.style.color = new Color(0.85f, 0.85f, 0.85f));

                    pathLabel.RegisterCallback<ClickEvent>(e =>
                    {
                        var asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(cleanPath);
                        if (asset != null)
                        {
                            EditorGUIUtility.PingObject(asset);
                        }
                    });
                    row.Add(pathLabel);

                    // Discard button for this item
                    var itemDiscardBtn = new Button();
                    itemDiscardBtn.AddToClassList("rex-result-delete-btn");
                    itemDiscardBtn.tooltip = "Discard changes for this file";
                    
                    var discardIcon = new VisualElement();
                    discardIcon.AddToClassList("rex-result-delete-icon");
                    itemDiscardBtn.Add(discardIcon);

                    itemDiscardBtn.clicked += async () =>
                    {
                        if (EditorUtility.DisplayDialog("Discard Changes", $"Are you sure you want to discard changes for:\n{cleanPath}?\nThis action cannot be undone.", "Yes", "No"))
                        {
                            SetUIExecuting(true);
                            await DiscardChangesAsync(fileLine);
                            SetUIExecuting(false);
                            await RefreshStatusAsync();
                        }
                    };
                    row.Add(itemDiscardBtn);

                    changedFilesScroll.Add(row);
                }
            }

            UpdateCommitButtonState();
        }

        private void SelectAllChangedFiles()
        {
            deselectedFiles.Clear();
            RebuildChangedFilesListUI();
        }

        private void DeselectAllChangedFiles()
        {
            foreach (var fileLine in currentChangedFileLines)
            {
                string cleanPath = GetFilePathFromLine(fileLine);
                if (!string.IsNullOrEmpty(cleanPath))
                {
                    deselectedFiles.Add(cleanPath);
                }
            }
            RebuildChangedFilesListUI();
        }

        private string UnquotePath(string p)
        {
            if (string.IsNullOrEmpty(p)) return string.Empty;
            p = p.Trim();
            if (p.StartsWith("\"") && p.EndsWith("\"") && p.Length >= 2)
            {
                p = p.Substring(1, p.Length - 2);
            }
            return p.Trim();
        }

        private string GetFilePathFromLine(string fileLine)
        {
            if (fileLine.Length < 3) return string.Empty;
            string prefix = fileLine.Substring(0, 2);
            string path = fileLine.Substring(2).Trim();
            
            path = UnquotePath(path);
            string cleanPath = path;
            if (prefix.Contains("R")) // Renamed "old -> new"
            {
                int arrow = path.IndexOf("->");
                if (arrow != -1)
                {
                    cleanPath = path.Substring(arrow + 2).Trim();
                    cleanPath = UnquotePath(cleanPath);
                }
            }
            return cleanPath;
        }

        private List<string> GetPathsFromLine(string fileLine)
        {
            var paths = new List<string>();
            if (fileLine.Length < 3) return paths;
            string prefix = fileLine.Substring(0, 2);
            string path = fileLine.Substring(2).Trim();
            
            path = UnquotePath(path);
            if (prefix.Contains("R")) // Renamed "old -> new"
            {
                int arrow = path.IndexOf("->");
                if (arrow != -1)
                {
                    string oldPath = path.Substring(0, arrow).Trim();
                    string newPath = path.Substring(arrow + 2).Trim();
                    paths.Add(UnquotePath(oldPath));
                    paths.Add(UnquotePath(newPath));
                }
                else
                {
                    paths.Add(path);
                }
            }
            else
            {
                paths.Add(path);
            }
            return paths;
        }

        private async Task DiscardChangesAsync(string fileLine)
        {
            string prefix = fileLine.Substring(0, 2);
            var paths = GetPathsFromLine(fileLine);
            foreach (var path in paths)
            {
                if (prefix.Contains("?"))
                {
                    DeleteFileOrDirectory(path);
                }
                else
                {
                    // 1. Unstage the file
                    await GitRunner.RunCommandAsync($"reset HEAD -- \"{path}\"", null, null);
                    // 2. Try checkout from HEAD
                    int exitCode = await GitRunner.RunCommandAsync($"checkout HEAD -- \"{path}\"", null, null);
                    // 3. If it wasn't in HEAD (failed checkout), delete it
                    if (exitCode != 0)
                    {
                        DeleteFileOrDirectory(path);
                    }
                }
            }
        }

        private void DeleteFileOrDirectory(string path)
        {
            string repoRoot = GitRunner.FindRepositoryRoot();
            string fullPath = Path.Combine(repoRoot, path).Replace("\\", "/");
            
            // If it's an asset in the project, try AssetDatabase.DeleteAsset
            string projectRoot = Directory.GetCurrentDirectory().Replace("\\", "/");
            if (fullPath.StartsWith(projectRoot, StringComparison.OrdinalIgnoreCase))
            {
                string relativeAssetPath = fullPath.Substring(projectRoot.Length).TrimStart('/');
                if (relativeAssetPath.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase) || 
                    relativeAssetPath.StartsWith("Packages/", StringComparison.OrdinalIgnoreCase))
                {
                    if (AssetDatabase.DeleteAsset(relativeAssetPath))
                    {
                        return;
                    }
                }
            }

            // Fallback/Non-asset deletion
            if (File.Exists(fullPath))
            {
                File.Delete(fullPath);
                if (File.Exists(fullPath + ".meta"))
                {
                    File.Delete(fullPath + ".meta");
                }
            }
            else if (Directory.Exists(fullPath))
            {
                Directory.Delete(fullPath, true);
                if (File.Exists(fullPath + ".meta"))
                {
                    File.Delete(fullPath + ".meta");
                }
            }
        }

        private async Task RunDiscardSelectedAsync()
        {
            var selectedLines = new List<string>();
            foreach (var fileLine in currentChangedFileLines)
            {
                string cleanPath = GetFilePathFromLine(fileLine);
                if (!deselectedFiles.Contains(cleanPath))
                {
                    selectedLines.Add(fileLine);
                }
            }

            if (selectedLines.Count == 0)
            {
                Log("No files selected to discard.");
                return;
            }

            string fileListStr = string.Join("\n", selectedLines.Select(line => GetFilePathFromLine(line)));
            if (EditorUtility.DisplayDialog("Discard Selected Changes", 
                $"Are you sure you want to discard changes for the {selectedLines.Count} selected file(s)?\n\n{fileListStr}\n\nThis action cannot be undone.", 
                "Yes", "No"))
            {
                SetUIExecuting(true);
                Log("> Discarding selected files...");
                foreach (var fileLine in selectedLines)
                {
                    await DiscardChangesAsync(fileLine);
                }
                Log("Discard completed.");
                SetUIExecuting(false);
                await RefreshStatusAsync();
            }
        }

        private void OnEditorUpdate()
        {
            if (!GitRunner.HasGitRepository()) return;

            double currentTime = EditorApplication.timeSinceStartup;

            if (isExecuting)
            {
                if (currentTime - lastSpinnerTime > 0.15)
                {
                    lastSpinnerTime = currentTime;
                    spinnerIndex = (spinnerIndex + 1) % SpinnerFrames.Length;
                    syncStatusLabel.text = $"Executing Git command... [{SpinnerFrames[spinnerIndex]}]";
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
                string cleanPath = GetFilePathFromLine(fileLine);
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

            int exitCode = await GitRunner.RunCommandAsync("fetch", 
                line => { if (!silent) Log(line); }, 
                line => { if (!silent) LogError(line); }
            );

            if (!silent)
            {
                Log($"Fetch finished with exit code {exitCode}");
                SetUIExecuting(false);
            }
            await RefreshStatusAsync();
        }

        private async Task RunPullAsync()
        {
            if (isExecuting) return;
            SetUIExecuting(true);
            Log("> git pull");

            int exitCode = await GitRunner.RunCommandAsync("pull", Log, LogError);
            Log($"Pull finished with exit code {exitCode}");
            
            SetUIExecuting(false);
            await RefreshStatusAsync();
        }

        private async Task RunPushAsync()
        {
            if (isExecuting) return;
            SetUIExecuting(true);
            Log("> git push");

            int exitCode = await GitRunner.RunCommandAsync("push", Log, LogError);
            Log($"Push finished with exit code {exitCode}");
            
            SetUIExecuting(false);
            await RefreshStatusAsync();
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

            var selectedLines = new List<string>();
            foreach (var fileLine in currentChangedFileLines)
            {
                string cleanPath = GetFilePathFromLine(fileLine);
                if (!deselectedFiles.Contains(cleanPath))
                {
                    selectedLines.Add(fileLine);
                }
            }

            if (selectedLines.Count == 0)
            {
                Log("Error: No files selected to commit.");
                return;
            }

            SetUIExecuting(true);
            
            Log("> git reset");
            await GitRunner.RunCommandAsync("reset", Log, LogError);

            var pathsToStage = new List<string>();
            foreach (var line in selectedLines)
            {
                pathsToStage.AddRange(GetPathsFromLine(line));
            }

            string addArgs = "add -- " + string.Join(" ", pathsToStage.Select(p => $"\"{p}\""));
            Log($"> git {addArgs}");
            int addExit = await GitRunner.RunCommandAsync(addArgs, Log, LogError);
            
            if (addExit == 0)
            {
                string escapedMessage = message.Replace("\"", "\\\"");
                Log($"> git commit -m \"{escapedMessage}\"");
                int commitExit = await GitRunner.RunCommandAsync($"commit -m \"{escapedMessage}\"", Log, LogError);
                
                if (commitExit == 0)
                {
                    Log("Commit successful.");
                    commitMsgField.value = "";
                }
                else
                {
                    LogError($"Commit failed with exit code {commitExit}");
                }
            }
            else
            {
                LogError($"Stage failed with exit code {addExit}");
            }

            SetUIExecuting(false);
            await RefreshStatusAsync();
        }
    }
}
