using System;
using System.IO;
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
        private TextField commitMsgField;
        private ScrollView changedFilesScroll;
        
        private VisualElement mainContentContainer;
        private VisualElement noRepoContainer;
        
        private bool isExecuting = false;
        private double lastFetchTime = 0.0f;
        private const double FetchInterval = 60.0; // 60 seconds

        private static readonly string[] SpinnerFrames = { "/", "-", "\\", "|" };
        private int spinnerIndex = 0;
        private double lastSpinnerTime = 0.0;

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
                "Commit: Stage modified files (git add -A) and commit changes.",
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

            // --- CHANGED FILES LIST ---
            var changedFilesBox = new VisualElement();
            changedFilesBox.AddToClassList("rex-box");
            changedFilesBox.style.marginTop = 6;
            
            var changedFilesLabel = new Label("CHANGED FILES");
            changedFilesLabel.AddToClassList("rex-section-label");
            changedFilesBox.Add(changedFilesLabel);

            changedFilesScroll = new ScrollView(ScrollViewMode.Vertical);
            changedFilesScroll.style.height = 100;
            changedFilesScroll.style.backgroundColor = new Color(0.12f, 0.12f, 0.12f, 0.5f);
            changedFilesScroll.style.paddingTop = 4;
            changedFilesScroll.style.paddingBottom = 4;
            changedFilesScroll.style.paddingLeft = 4;
            changedFilesScroll.style.paddingRight = 4;
            changedFilesScroll.style.borderTopWidth = 1;
            changedFilesScroll.style.borderBottomWidth = 1;
            changedFilesScroll.style.borderLeftWidth = 1;
            changedFilesScroll.style.borderRightWidth = 1;
            changedFilesScroll.style.borderTopColor = new Color(0.2f, 0.2f, 0.2f);
            changedFilesScroll.style.borderBottomColor = new Color(0.2f, 0.2f, 0.2f);
            changedFilesScroll.style.borderLeftColor = new Color(0.2f, 0.2f, 0.2f);
            changedFilesScroll.style.borderRightColor = new Color(0.2f, 0.2f, 0.2f);
            
            changedFilesBox.Add(changedFilesScroll);
            mainContentContainer.Add(changedFilesBox);

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

            commitBtn = new Button { text = "STAGE & COMMIT ALL" };
            commitBtn.AddToClassList("rex-action-button");
            commitBtn.AddToClassList("rex-action-button--pack");
            commitBtn.style.marginTop = 8;
            commitBtn.style.height = 36;
            commitBtn.clicked += async () => await RunCommitAsync();
            opsBox.Add(commitBtn);

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
                changedFilesScroll.Clear();
                var changedFiles = await GitRunner.GetChangedFilesAsync();
                if (changedFiles.Count == 0)
                {
                    var cleanLabel = new Label("No changed files (Working directory clean)");
                    cleanLabel.style.color = new Color(0.5f, 0.5f, 0.5f);
                    cleanLabel.style.fontSize = 10;
                    cleanLabel.style.paddingLeft = 4;
                    changedFilesScroll.Add(cleanLabel);
                }
                else
                {
                    foreach (var fileLine in changedFiles)
                    {
                        if (fileLine.Length < 3) continue;

                        string prefix = fileLine.Substring(0, 2);
                        string path = fileLine.Substring(2).Trim();

                        var row = new VisualElement();
                        row.style.flexDirection = FlexDirection.Row;
                        row.style.alignItems = Align.Center;
                        row.style.marginBottom = 2;

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

                        var pathLabel = new Label(path);
                        pathLabel.style.flexGrow = 1;
                        pathLabel.style.fontSize = 10;
                        pathLabel.style.color = new Color(0.85f, 0.85f, 0.85f);
                        pathLabel.style.paddingLeft = 4;

                        // Hover effect
                        pathLabel.RegisterCallback<MouseOverEvent>(e => pathLabel.style.color = new Color(0.4f, 0.8f, 1.0f));
                        pathLabel.RegisterCallback<MouseOutEvent>(e => pathLabel.style.color = new Color(0.85f, 0.85f, 0.85f));

                        string cleanPath = path;
                        if (prefix.Contains("R")) // Renamed "old -> new"
                        {
                            int arrow = path.IndexOf("->");
                            if (arrow != -1)
                            {
                                cleanPath = path.Substring(arrow + 2).Trim();
                            }
                        }

                        pathLabel.RegisterCallback<ClickEvent>(e =>
                        {
                            var asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(cleanPath);
                            if (asset != null)
                            {
                                EditorGUIUtility.PingObject(asset);
                            }
                        });

                        row.Add(pathLabel);
                        changedFilesScroll.Add(row);
                    }
                }
            }
            
            // Sync status to playmode toolbar button
            GitToolbarExtender.ForceRefresh();
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
            commitBtn.SetEnabled(!executing);
            commitMsgField.SetEnabled(!executing);

            // Set the global network flag on GitRunner
            GitRunner.IsRunningNetworkCommand = executing;
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

            SetUIExecuting(true);
            
            Log("> git add -A");
            int addExit = await GitRunner.RunCommandAsync("add -A", Log, LogError);
            
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
