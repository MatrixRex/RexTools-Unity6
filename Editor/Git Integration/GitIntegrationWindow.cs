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
        private List<string> rawChangedFileLines = new List<string>();

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
            mainContentContainer.style.flexGrow = 1;
            noRepoContainer = new VisualElement();
            noRepoContainer.style.flexGrow = 1;

            root.Add(mainContentContainer);
            root.Add(noRepoContainer);

            BuildMainLayout();
            BuildNoRepoLayout();

            // Initial check
            RefreshLayout();
        }

        private void BuildNoRepoLayout()
        {
            var noRepoScroll = new ScrollView(ScrollViewMode.Vertical);
            noRepoScroll.style.flexGrow = 1;
            noRepoContainer.Add(noRepoScroll);

            var box = new VisualElement();
            box.AddToClassList("rex-box");
            box.style.alignItems = Align.Center;
            box.style.paddingTop = 20;
            box.style.paddingBottom = 20;
            box.style.paddingLeft = 20;
            box.style.paddingRight = 20;
            noRepoScroll.Add(box);

            var warningLabel = new Label("No Git repository found in the project root or parent directories.");
            warningLabel.style.whiteSpace = WhiteSpace.Normal;
            warningLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
            warningLabel.style.marginBottom = 15;
            box.Add(warningLabel);

            var detectBtn = new Button { text = "Scan for Repository" };
            detectBtn.AddToClassList("rex-action-button");
            detectBtn.AddToClassList("rex-action-button--pack");
            detectBtn.style.width = 200;
            detectBtn.style.height = 30;
            detectBtn.clicked += RefreshLayout;
            box.Add(detectBtn);
        }

        private void BuildMainLayout()
        {
            // --- SCROLLABLE CONTENT AREA ---
            var mainScrollView = new ScrollView(ScrollViewMode.Vertical);
            mainScrollView.style.flexGrow = 1;
            mainScrollView.style.marginTop = 4;
            mainScrollView.style.marginBottom = 4;
            mainScrollView.contentContainer.style.flexGrow = 1; // Allow children to expand
            mainContentContainer.Add(mainScrollView);

            // --- REPOSITORY INFO ---
            var infoBox = new VisualElement();
            infoBox.AddToClassList("rex-box");
            infoBox.style.flexShrink = 0; // Prevent status area from shrinking

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

            mainScrollView.Add(infoBox);

            // --- CHANGED FILES LIST (RexFoldout + RexList) ---
            changedFilesFoldout = new RexFoldout("Changed Files", count: 0, defaultExpanded: true);
            changedFilesFoldout.style.marginTop = 6;
            changedFilesFoldout.style.flexGrow = 1;
            changedFilesFoldout.style.flexShrink = 0;
            changedFilesFoldout.contentContainer.style.flexGrow = 1;
            changedFilesFoldout.contentContainer.style.flexShrink = 0;

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
            changedFilesScroll.style.flexGrow = 1;
            changedFilesScroll.style.flexShrink = 0;
            changedFilesScroll.style.minHeight = 120; // Default minimum height for the list
            
            changedFilesFoldout.Add(changedFilesScroll);
            mainScrollView.Add(changedFilesFoldout);

            // --- OPERATIONS PANEL ---
            var opsBox = new VisualElement();
            opsBox.AddToClassList("rex-box");
            opsBox.style.flexShrink = 0; // Prevent operations area from shrinking
            opsBox.style.minHeight = 180; // Set minimum height to prevent squashing

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

            branchStatusLabel.text = $"Branch: {branch}";
            
            // Rebuild the changed files list first to get correct count
            if (changedFilesScroll != null)
            {
                rawChangedFileLines = await GitRunner.GetChangedFilesAsync();
                currentChangedFileLines = FilterAndDeduplicateChangedFiles(rawChangedFileLines);
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

        private List<string> FilterAndDeduplicateChangedFiles(List<string> rawLines)
        {
            var cleanPathsSeen = new HashSet<string>();
            var filtered = new List<string>();

            foreach (var line in rawLines)
            {
                if (line.Length < 3) continue;
                string cleanPath = GetFilePathFromLine(line);
                if (string.IsNullOrEmpty(cleanPath)) continue;

                // Skip directories directly listed by git
                if (cleanPath.EndsWith("/") || cleanPath.EndsWith("\\")) continue;

                // Strip .meta if present
                string baseCleanPath = cleanPath;
                if (cleanPath.EndsWith(".meta", StringComparison.OrdinalIgnoreCase))
                {
                    baseCleanPath = cleanPath.Substring(0, cleanPath.Length - 5);
                }

                // If the base clean path is a directory (e.g. folder .meta), skip it
                if (IsDirectoryPath(baseCleanPath)) continue;

                if (!cleanPathsSeen.Contains(baseCleanPath))
                {
                    cleanPathsSeen.Add(baseCleanPath);
                    
                    // If this was a .meta entry itself, format a new porcelain line
                    // that points to the base asset but keeps the status prefix.
                    if (cleanPath.EndsWith(".meta", StringComparison.OrdinalIgnoreCase))
                    {
                        string prefix = line.Substring(0, 2);
                        filtered.Add($"{prefix} {baseCleanPath}");
                    }
                    else
                    {
                        filtered.Add(line);
                    }
                }
            }

            return filtered;
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

                    // Asset Icon
                    Texture iconTexture = GetAssetIcon(cleanPath);
                    if (iconTexture != null)
                    {
                        var assetIcon = new Image();
                        assetIcon.image = iconTexture;
                        assetIcon.style.width = 16;
                        assetIcon.style.height = 16;
                        assetIcon.style.marginRight = 4;
                        row.Add(assetIcon);
                    }

                    // File path label
                    var pathLabel = new Label(path);
                    pathLabel.AddToClassList("rex-result-name-btn");
                    pathLabel.style.flexGrow = 1;
                    pathLabel.style.flexShrink = 1;
                    pathLabel.style.overflow = Overflow.Hidden;
                    pathLabel.style.textOverflow = TextOverflow.Ellipsis;
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
                            Selection.activeObject = asset;
                            EditorGUIUtility.PingObject(asset);
                        }
                    });
                    row.Add(pathLabel);

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

        private bool IsDirectoryPath(string basePath)
        {
            string repoRoot = GitRunner.FindRepositoryRoot();
            string fullPath = Path.Combine(repoRoot, basePath).Replace("\\", "/");
            if (Directory.Exists(fullPath)) return true;

            if (basePath.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase) || 
                basePath.StartsWith("Packages/", StringComparison.OrdinalIgnoreCase))
            {
                if (string.IsNullOrEmpty(Path.GetExtension(basePath)))
                {
                    return true;
                }
            }

            return false;
        }

        private List<string> GetParentDirectories(string path)
        {
            var parents = new List<string>();
            string dir = Path.GetDirectoryName(path);
            while (!string.IsNullOrEmpty(dir))
            {
                dir = dir.Replace("\\", "/");
                if (dir == "Assets" || dir == "Packages" || dir == "") break;
                parents.Add(dir);
                dir = Path.GetDirectoryName(dir);
            }
            return parents;
        }

        private Texture GetAssetIcon(string cleanPath)
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

        private async Task DiscardChangesForCleanPathAsync(string cleanPath)
        {
            var rawLinesToDiscard = new List<string>();
            foreach (var rawLine in rawChangedFileLines)
            {
                string rawCleanPath = GetFilePathFromLine(rawLine);
                string baseCleanPath = rawCleanPath;
                if (rawCleanPath.EndsWith(".meta", StringComparison.OrdinalIgnoreCase))
                {
                    baseCleanPath = rawCleanPath.Substring(0, rawCleanPath.Length - 5);
                }

                if (baseCleanPath == cleanPath)
                {
                    rawLinesToDiscard.Add(rawLine);
                }
            }

            // Check parent directories of this cleanPath to see if we should discard their .meta files
            var parents = GetParentDirectories(cleanPath);
            foreach (var parent in parents)
            {
                string parentMeta = parent + ".meta";
                
                // Check if this parent .meta is modified/untracked
                bool isParentMetaChanged = false;
                string parentMetaRawLine = null;
                foreach (var rawLine in rawChangedFileLines)
                {
                    string rawCleanPath = GetFilePathFromLine(rawLine);
                    if (rawCleanPath == parentMeta)
                    {
                        isParentMetaChanged = true;
                        parentMetaRawLine = rawLine;
                        break;
                    }
                }

                if (isParentMetaChanged)
                {
                    // Check if there are any OTHER changed files under this parent folder
                    // (excluding the current cleanPath we are discarding, and excluding the parent .meta files themselves)
                    bool hasOtherChanges = false;
                    foreach (var rawLine in rawChangedFileLines)
                    {
                        string rawCleanPath = GetFilePathFromLine(rawLine);
                        
                        // Skip the file we are currently discarding and its .meta
                        string baseClean = rawCleanPath;
                        if (rawCleanPath.EndsWith(".meta", StringComparison.OrdinalIgnoreCase))
                        {
                            baseClean = rawCleanPath.Substring(0, rawCleanPath.Length - 5);
                        }
                        if (baseClean == cleanPath) continue;

                        // Skip parent directories and their .meta files
                        if (baseClean == parent || baseClean.StartsWith(parent + "/", StringComparison.OrdinalIgnoreCase))
                        {
                            // If baseClean is a parent directory (no extension) or its .meta, skip it
                            // otherwise it's a real file change under parent
                            string ext = Path.GetExtension(baseClean);
                            if (string.IsNullOrEmpty(ext)) continue;
                            
                            hasOtherChanges = true;
                            break;
                        }
                    }

                    if (!hasOtherChanges && parentMetaRawLine != null)
                    {
                        rawLinesToDiscard.Add(parentMetaRawLine);
                    }
                }
            }

            foreach (var fileLine in rawLinesToDiscard)
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
            var selectedCleanPaths = new List<string>();
            foreach (var fileLine in currentChangedFileLines)
            {
                string cleanPath = GetFilePathFromLine(fileLine);
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
                foreach (var cleanPath in selectedCleanPaths)
                {
                    await DiscardChangesForCleanPathAsync(cleanPath);
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

            var selectedCleanPaths = new HashSet<string>();
            foreach (var fileLine in currentChangedFileLines)
            {
                string cleanPath = GetFilePathFromLine(fileLine);
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
            
            Log("> git reset");
            await GitRunner.RunCommandAsync("reset", Log, LogError);

            var pathsToStage = new List<string>();
            foreach (var rawLine in rawChangedFileLines)
            {
                string rawCleanPath = GetFilePathFromLine(rawLine);
                string baseCleanPath = rawCleanPath;
                if (rawCleanPath.EndsWith(".meta", StringComparison.OrdinalIgnoreCase))
                {
                    baseCleanPath = rawCleanPath.Substring(0, rawCleanPath.Length - 5);
                }

                if (selectedCleanPaths.Contains(baseCleanPath))
                {
                    pathsToStage.AddRange(GetPathsFromLine(rawLine));
                }
            }

            // Automatically find and stage parent folder .meta files if they are modified/untracked
            var parentMetasToStage = new HashSet<string>();
            foreach (var path in pathsToStage)
            {
                var parents = GetParentDirectories(path);
                foreach (var parent in parents)
                {
                    string parentMeta = parent + ".meta";
                    foreach (var rawLine in rawChangedFileLines)
                    {
                        string rawCleanPath = GetFilePathFromLine(rawLine);
                        if (rawCleanPath == parentMeta)
                        {
                            parentMetasToStage.Add(parentMeta);
                            break;
                        }
                    }
                }
            }
            pathsToStage.AddRange(parentMetasToStage);

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
