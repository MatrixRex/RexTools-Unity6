using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEditor.UIElements;
using System.IO;
using System.Collections.Generic;
using System.Linq;

namespace RexTools.UnusedAssetFinder.Editor
{
    public class UnusedAssetsFinderNew : EditorWindow
    {
        private string folderPath = "Assets";
        private bool recursiveSearch = false;
        private bool showHelp = false;

        private Dictionary<string, List<string>> categorizedUnusedFiles = new Dictionary<string, List<string>>();
        private string[] tabs = { "Textures", "Prefabs", "Models", "Other" };
        private int currentTabIndex = 0;

        // UI Elements
        private VisualElement helpBox;
        private TextField pathField;
        private Toggle recursiveToggle;
        private VisualElement resultsContainer;
        private ScrollView resultsScroll;
        private VisualElement tabsContainer;
        private List<Button> tabButtons = new List<Button>();
        private Button runButton;
        private Button deleteAllButton;

        private Foldout subfolderFoldout;
        private List<FolderNode> subfolderTree = new List<FolderNode>();
        private int progressId = -1;

        [MenuItem("Tools/Rex Tools/Unused Assets Finder")]
        public static void ShowWindow()
        {
            var window = GetWindow<UnusedAssetsFinderNew>("Unused Assets Finder");
            window.minSize = new Vector2(400, 600);
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
            foreach (var path in possiblePaths) {
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

            var titleLabel = new Label("Unused Assets Finder");
            titleLabel.AddToClassList("rex-tool-title");
            brandStack.Add(titleLabel);

            header.Add(brandStack);

            var helpBtn = new Button { text = "?" };
            helpBtn.AddToClassList("rex-help-btn");
            header.Add(helpBtn);

            root.Add(header);

            // --- HELP BOX ---
            helpBox = new VisualElement();
            helpBox.AddToClassList("rex-hidden");
            helpBox.AddToClassList("rex-box");
            helpBox.AddToClassList("rex-help-box");
            
            var helpTitle = new Label("HOW TO USE:");
            helpTitle.AddToClassList("rex-help-text-title");
            helpBox.Add(helpTitle);

            Label help1 = new Label("• Drag and drop folders onto the path field.");
            help1.AddToClassList("rex-help-text-item");
            helpBox.Add(help1);

            Label help2 = new Label("• Click on file names to focus them in the Project window.");
            help2.AddToClassList("rex-help-text-item");
            helpBox.Add(help2);

            Label help3 = new Label("• Use the trash icon to delete specific assets.");
            help3.AddToClassList("rex-help-text-item");
            helpBox.Add(help3);

            Label help4 = new Label("• 'Delete All' will remove all assets in the CURRENTLY SELECTED tab.");
            help4.AddToClassList("rex-help-text-item");
            helpBox.Add(help4);
            
            root.Add(helpBox);

            helpBtn.clicked += () => {
                showHelp = !showHelp;
                helpBox.style.display = showHelp ? DisplayStyle.Flex : DisplayStyle.None;
            };

            // --- SETTINGS SECTION ---
            var settingsBox = new VisualElement();
            settingsBox.AddToClassList("rex-box");
            
            var pathRow = new VisualElement();
            pathRow.AddToClassList("rex-row");
            var folderLabel = new Label("Folder:");
            folderLabel.AddToClassList("rex-label-w50");
            pathRow.Add(folderLabel);
            pathField = new TextField { value = folderPath };
            pathField.AddToClassList("rex-flex-grow");
            pathField.RegisterValueChangedCallback(e => folderPath = e.newValue);
            
            // Drag and Drop for Path Field
            pathField.RegisterCallback<DragUpdatedEvent>(e => DragAndDrop.visualMode = DragAndDropVisualMode.Copy);
            pathField.RegisterCallback<DragPerformEvent>(e => {
                DragAndDrop.AcceptDrag();
                string path = DragAndDrop.paths.FirstOrDefault();
                if (!string.IsNullOrEmpty(path) && AssetDatabase.IsValidFolder(path)) {
                    folderPath = path;
                    pathField.value = path;
                }
            });

            pathRow.Add(pathField);
            settingsBox.Add(pathRow);

            recursiveToggle = new Toggle("Recursive Search") { value = recursiveSearch };
            recursiveToggle.RegisterValueChangedCallback(e => {
                recursiveSearch = e.newValue;
                RefreshSubfolders();
            });
            settingsBox.Add(recursiveToggle);

            subfolderFoldout = new Foldout { text = "Subfolders", value = false };
            subfolderFoldout.AddToClassList("rex-box");
            subfolderFoldout.AddToClassList("rex-hidden");
            settingsBox.Add(subfolderFoldout);

            root.Add(settingsBox);

            // --- ACTION BUTTONS ---
            var actionRow = new VisualElement();
            actionRow.AddToClassList("rex-row");
            actionRow.AddToClassList("rex-margin-top-10");
            
            runButton = new Button { text = "FIND UNUSED ASSETS" };
            runButton.AddToClassList("rex-action-button");
            runButton.AddToClassList("rex-flex-grow");
            runButton.clicked += RunSearch;
            actionRow.Add(runButton);

            deleteAllButton = new Button { text = "DELETE ALL" };
            deleteAllButton.AddToClassList("rex-action-button");
            deleteAllButton.AddToClassList("rex-delete-all-btn");
            deleteAllButton.clicked += DeleteAllVisible;
            deleteAllButton.SetEnabled(false);
            actionRow.Add(deleteAllButton);

            root.Add(actionRow);

            // --- TABS ---
            tabsContainer = new VisualElement();
            tabsContainer.AddToClassList("rex-tabs-container");
            for (int i = 0; i < tabs.Length; i++) {
                int index = i;
                var btn = new Button { text = tabs[i] };
                btn.AddToClassList("rex-tab-button");
                btn.clicked += () => SwitchTab(index);
                tabButtons.Add(btn);
                tabsContainer.Add(btn);
            }
            root.Add(tabsContainer);

            // --- RESULTS AREA ---
            resultsContainer = new VisualElement();
            resultsContainer.AddToClassList("rex-box");
            resultsContainer.AddToClassList("rex-result-list");

            resultsScroll = new ScrollView();
            resultsScroll.AddToClassList("rex-flex-grow");
            resultsContainer.Add(resultsScroll);

            root.Add(resultsContainer);

            SwitchTab(0);
            RefreshSubfolders();
        }

        private void RefreshSubfolders()
        {
            subfolderFoldout.Clear();
            subfolderFoldout.style.display = recursiveSearch ? DisplayStyle.Flex : DisplayStyle.None;
            if (!recursiveSearch) return;

            if (Directory.Exists(folderPath)) {
                subfolderTree = BuildFolderTree(folderPath);
                DisplayFolderTree(subfolderTree, subfolderFoldout);
            }
        }

        private List<FolderNode> BuildFolderTree(string rootPath)
        {
            List<FolderNode> tree = new List<FolderNode>();
            try {
                foreach (string dir in Directory.GetDirectories(rootPath)) {
                    var node = new FolderNode {
                        Name = Path.GetFileName(dir),
                        Subfolders = BuildFolderTree(dir)
                    };
                    tree.Add(node);
                }
            } catch { } // Ignore issues with specific folders
            return tree;
        }

        private void DisplayFolderTree(List<FolderNode> nodes, VisualElement parent, int indent = 0)
        {
            foreach (var node in nodes) {
                var row = new Label($"- {node.Name}");
                row.AddToClassList("rex-subfolder-item");
                row.style.paddingLeft = indent * 15;
                parent.Add(row);
                if (node.Subfolders.Count > 0) {
                    DisplayFolderTree(node.Subfolders, parent, indent + 1);
                }
            }
        }

        private void SwitchTab(int index)
        {
            currentTabIndex = index;
            for (int i = 0; i < tabButtons.Count; i++) {
                tabButtons[i].RemoveFromClassList("rex-tab-button--active");
                tabButtons[i].RemoveFromClassList("rex-tab-button--inactive");
                tabButtons[i].AddToClassList(i == index ? "rex-tab-button--active" : "rex-tab-button--inactive");
            }
            RefreshResultsList();
        }

        private void RunSearch()
        {
            if (!Directory.Exists(folderPath)) {
                Debug.LogError($"[RexTools] Folder path does not exist: {folderPath}");
                return;
            }

            categorizedUnusedFiles.Clear();
            progressId = Progress.Start("Finding Unused Assets", "Scanning scene references...", Progress.Options.Indefinite);
            
            // Run in background next frame
            EditorApplication.delayCall += ProcessAssets;
        }

        private void ProcessAssets()
        {
            try {
                var files = Directory.GetFiles(folderPath, "*.*", recursiveSearch ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly)
                                     .Where(file => !file.EndsWith(".meta"))
                                     .ToList();

                var sceneReferences = new HashSet<string>();
                var allObjects = FindObjectsOfType<UnityEngine.Object>();
                
                float totalObj = allObjects.Length;
                for (int i = 0; i < allObjects.Length; i++) {
                    if (i % 100 == 0) Progress.Report(progressId, i / totalObj, $"Checking dependencies: {allObjects[i].name}");
                    
                    var dependencies = EditorUtility.CollectDependencies(new[] { allObjects[i] });
                    foreach (var dependency in dependencies) {
                        var path = AssetDatabase.GetAssetPath(dependency);
                        if (!string.IsNullOrEmpty(path)) sceneReferences.Add(path);
                    }
                }

                foreach (var file in files) {
                    var relativePath = file.Replace(Application.dataPath, "Assets").Replace("\\", "/");
                    if (!sceneReferences.Contains(relativePath)) {
                        CategorizeFile(relativePath);
                    }
                }
            } finally {
                Progress.Finish(progressId);
                progressId = -1;
                RefreshResultsList();
            }
        }

        private void CategorizeFile(string filePath)
        {
            string extension = Path.GetExtension(filePath).ToLower();
            string category = extension switch {
                ".png" or ".jpg" or ".jpeg" or ".tga" or ".bmp" or ".psd" or ".tif" or ".tiff" => "Textures",
                ".prefab" => "Prefabs",
                ".fbx" or ".obj" or ".blend" or ".dae" => "Models",
                _ => "Other",
            };

            if (!categorizedUnusedFiles.ContainsKey(category)) categorizedUnusedFiles[category] = new List<string>();
            categorizedUnusedFiles[category].Add(filePath);
        }

        private void RefreshResultsList()
        {
            resultsScroll.Clear();
            string currentTabName = tabs[currentTabIndex];
            
            if (categorizedUnusedFiles.TryGetValue(currentTabName, out var files) && files.Count > 0) {
                deleteAllButton.SetEnabled(true);
                foreach (var file in files) {
                    var item = CreateResultItem(file);
                    resultsScroll.Add(item);
                }
            } else {
                deleteAllButton.SetEnabled(false);
                var emptyLabel = new Label("No unused assets found in this category.");
                emptyLabel.AddToClassList("rex-empty-label");
                resultsScroll.Add(emptyLabel);
            }
        }

        private VisualElement CreateResultItem(string path)
        {
            var row = new VisualElement();
            row.AddToClassList("rex-result-item");

            var fileName = Path.GetFileName(path);
            var nameBtn = new Button { text = fileName };
            nameBtn.AddToClassList("rex-result-name-btn");
            nameBtn.clicked += () => PingAsset(path);
            row.Add(nameBtn);

            var deleteBtn = new Button();
            deleteBtn.AddToClassList("rex-result-delete-btn");
            
            var icon = new VisualElement();
            icon.AddToClassList("rex-result-delete-icon");
            icon.style.backgroundImage = (Texture2D)EditorGUIUtility.IconContent("TreeEditor.Trash").image;
            deleteBtn.Add(icon);
            
            deleteBtn.clicked += () => {
                if (DeleteAsset(path)) {
                    categorizedUnusedFiles[tabs[currentTabIndex]].Remove(path);
                    RefreshResultsList();
                }
            };
            row.Add(deleteBtn);

            return row;
        }

        private void PingAsset(string path)
        {
            var asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path);
            if (asset != null) EditorGUIUtility.PingObject(asset);
        }

        private bool DeleteAsset(string path)
        {
            if (EditorUtility.DisplayDialog("Delete Asset", $"Are you sure you want to delete {path}?", "Yes", "No")) {
                AssetDatabase.DeleteAsset(path);
                return true;
            }
            return false;
        }

        private void DeleteAllVisible()
        {
            string currentTabName = tabs[currentTabIndex];
            if (categorizedUnusedFiles.TryGetValue(currentTabName, out var files)) {
                if (EditorUtility.DisplayDialog("Delete All", $"Delete all {files.Count} unused {currentTabName} assets?", "Yes", "No")) {
                    AssetDatabase.StartAssetEditing();
                    try {
                        foreach (var file in files) AssetDatabase.DeleteAsset(file);
                    } finally {
                        AssetDatabase.StopAssetEditing();
                    }
                    categorizedUnusedFiles[currentTabName].Clear();
                    RefreshResultsList();
                }
            }
        }

        private class FolderNode
        {
            public string Name;
            public List<FolderNode> Subfolders = new List<FolderNode>();
        }
    }
}
