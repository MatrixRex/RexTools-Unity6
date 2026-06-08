using System;
using System.IO;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

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
        
        private ScrollView consoleScroll;
        private Label consoleLog;
        private Button clearLogBtn;

        private VisualElement mainContentContainer;
        private VisualElement noRepoContainer;
        
        private bool isExecuting = false;
        private double lastFetchTime = 0.0f;
        private const double FetchInterval = 60.0; // 60 seconds

        [MenuItem("Tools/Rex Tools/Git Integration")]
        public static void ShowWindow()
        {
            var window = GetWindow<GitIntegrationWindow>("Git Integration");
            window.minSize = new Vector2(380, 500);
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

            // --- BRANDED HEADER ---
            var header = new VisualElement();
            header.AddToClassList("rex-header-row");

            var brandStack = new VisualElement();
            brandStack.AddToClassList("rex-header-stack");

            var brandLabel = new Label("Rex Tools");
            brandLabel.AddToClassList("rex-brand-label");
            brandStack.Add(brandLabel);

            var titleLabel = new Label("Git Integration");
            titleLabel.AddToClassList("rex-tool-title");
            brandStack.Add(titleLabel);

            header.Add(brandStack);

            var helpBtn = new Button();
            helpBtn.AddToClassList("rex-help-btn");
            header.Add(helpBtn);

            root.Add(header);

            // --- HELP BOX ---
            var helpBox = new VisualElement();
            helpBox.AddToClassList("rex-help-box");
            helpBox.AddToClassList("rex-box");
            helpBox.AddToClassList("rex-hidden");

            var helpTitle = new Label("HOW TO USE:");
            helpTitle.AddToClassList("rex-help-text-title");
            helpBox.Add(helpTitle);

            helpBox.Add(new Label("• Fetch: Fetch updates from remote tracking branch.") { className = "rex-help-text-item" });
            helpBox.Add(new Label("• Pull: Integrates remote branch updates to local.") { className = "rex-help-text-item" });
            helpBox.Add(new Label("• Commit: Stage modified files (git add -A) and commit changes.") { className = "rex-help-text-item" });
            helpBox.Add(new Label("• Push: Upload committed changes to remote repository.") { className = "rex-help-text-item" });
            helpBox.Add(new Label("• Branch Status: Updates automatically when focus returns to Unity.") { className = "rex-help-text-item" });
            
            root.Add(helpBox);

            helpBtn.clicked += () =>
            {
                helpBox.ToggleInClassList("rex-hidden");
                helpBtn.ToggleInClassList("rex-help-btn--active");
            };

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
            noRepoContainer.style.padding = 20;

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

            // --- CONSOLE OUTPUT ---
            var consoleBox = new VisualElement();
            consoleBox.AddToClassList("rex-box");
            consoleBox.style.flexGrow = 1;

            var consoleHeaderRow = new VisualElement();
            consoleHeaderRow.AddToClassList("rex-row");
            consoleHeaderRow.style.justifyContent = Justify.SpaceBetween;

            var consoleLabel = new Label("CONSOLE LOG");
            consoleLabel.AddToClassList("rex-section-label");
            consoleHeaderRow.Add(consoleLabel);

            clearLogBtn = new Button { text = "Clear" };
            clearLogBtn.style.height = 16;
            clearLogBtn.style.fontSize = 9;
            clearLogBtn.clicked += () => consoleLog.text = "";
            consoleHeaderRow.Add(clearLogBtn);

            consoleBox.Add(consoleHeaderRow);

            consoleScroll = new ScrollView(ScrollViewMode.VerticalAndHorizontal);
            consoleScroll.style.flexGrow = 1;
            consoleScroll.style.backgroundColor = new Color(0.12f, 0.12f, 0.12f, 0.9f);
            consoleScroll.style.padding = 6;
            consoleScroll.style.marginTop = 4;
            consoleScroll.style.borderWidth = 1;
            consoleScroll.style.borderColor = new Color(0.2f, 0.2f, 0.2f);

            consoleLog = new Label("Git console initialized.\n");
            consoleLog.style.whiteSpace = WhiteSpace.Normal;
            consoleLog.style.fontSize = 11;
            consoleLog.style.unityFontDefinition = FontDefinition.FromSDFFont(null); // use monospace fallback if possible
            consoleScroll.Add(consoleLog);

            consoleBox.Add(consoleScroll);

            mainContentContainer.Add(consoleBox);
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
            
            // Sync status to playmode toolbar button
            GitToolbarExtender.ForceRefresh();
        }

        private void OnEditorUpdate()
        {
            if (!GitRunner.HasGitRepository() || isExecuting) return;

            double currentTime = EditorApplication.timeSinceStartup;
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
            clearLogBtn.SetEnabled(!executing);
        }

        private void Log(string text)
        {
            if (string.IsNullOrEmpty(text)) return;
            consoleLog.text += $"{text}\n";
            
            // Auto scroll to bottom
            EditorApplication.delayCall += () =>
            {
                consoleScroll.scrollOffset = new Vector2(0, float.MaxValue);
            };
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
                line => { if (!silent) Log($"[Err] {line}"); }
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

            int exitCode = await GitRunner.RunCommandAsync("pull", Log, line => Log($"[Err] {line}"));
            Log($"Pull finished with exit code {exitCode}");
            
            SetUIExecuting(false);
            await RefreshStatusAsync();
        }

        private async Task RunPushAsync()
        {
            if (isExecuting) return;
            SetUIExecuting(true);
            Log("> git push");

            int exitCode = await GitRunner.RunCommandAsync("push", Log, line => Log($"[Err] {line}"));
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
            int addExit = await GitRunner.RunCommandAsync("add -A", Log, line => Log($"[Err] {line}"));
            
            if (addExit == 0)
            {
                string escapedMessage = message.Replace("\"", "\\\"");
                Log($"> git commit -m \"{escapedMessage}\"");
                int commitExit = await GitRunner.RunCommandAsync($"commit -m \"{escapedMessage}\"", Log, line => Log($"[Err] {line}"));
                
                if (commitExit == 0)
                {
                    Log("Commit successful.");
                    commitMsgField.value = "";
                }
                else
                {
                    Log($"Commit failed with exit code {commitExit}");
                }
            }
            else
            {
                Log($"Stage failed with exit code {addExit}");
            }

            SetUIExecuting(false);
            await RefreshStatusAsync();
        }
    }
}
