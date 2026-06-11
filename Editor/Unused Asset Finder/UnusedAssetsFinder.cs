using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEditor.UIElements;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using RexTools.Editor.Core;

namespace RexTools.UnusedAssetFinder.Editor
{
    public class UnusedAssetsFinder : EditorWindow
    {
        private string folderPath = "Assets";
        private bool recursiveSearch = false;
        private bool showHelp = false;

        private Dictionary<string, List<string>> categorizedUnusedFiles = new Dictionary<string, List<string>>();
        private string[] tabs = { "Textures", "Prefabs", "Models", "Other" };
        private int currentTabIndex = 0;

        // UI Elements
        private RexHelpBox helpBox;
        private RexFolderSelector pathField;
        private Toggle recursiveToggle;
        private VisualElement resultsContainer;
        private ScrollView resultsScroll;
        private RexTabGroup tabGroup;
        private RexActionButton runButton;
        private RexActionButton deleteSelectedButton;

        private RexFoldout subfolderFoldout;
        private List<FolderNode> subfolderTree = new List<FolderNode>();
        private int progressId = -1;

        [MenuItem("Tools/Rex Tools/Unused Assets Finder")]
        public static void ShowWindow()
        {
            var window = GetWindow<UnusedAssetsFinder>("Unused Assets Finder");
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

            // --- BRANDED HEADER & HELP BOX ---
            helpBox = new RexHelpBox(
                "Drag and drop folders onto the path field.",
                "Click on file names to focus them in the Project window.",
                "Use the trash icon to delete specific assets.",
                "'Delete All' will remove all assets in the CURRENTLY SELECTED tab."
            );

            var header = new RexHeader("Unused Assets Finder", showHelpButton: true);
            header.OnHelpClicked += () => {
                showHelp = !showHelp;
                helpBox.ToggleVisibility();
                header.SetHelpButtonActive(showHelp);
            };

            root.Add(header);
            root.Add(helpBox);

            // --- MAIN SCROLLVIEW FOR CONTENT ---
            var mainScroll = new ScrollView(ScrollViewMode.Vertical);
            mainScroll.style.flexGrow = 1;
            root.Add(mainScroll);

            // --- SETTINGS SECTION ---
            var settingsBox = new VisualElement();
            settingsBox.AddToClassList("rex-box");
            
            var pathRow = new VisualElement();
            pathRow.AddToClassList("rex-row");
            var folderLabel = new Label("Folder:");
            folderLabel.AddToClassList("rex-label-w50");
            pathRow.Add(folderLabel);
            pathField = new RexFolderSelector();
            pathField.SetPathWithoutNotify(folderPath);
            pathField.AddToClassList("rex-flex-grow");
            pathField.OnValueChanged += path => {
                folderPath = path;
                RefreshSubfolders();
            };

            pathRow.Add(pathField);
            settingsBox.Add(pathRow);

            recursiveToggle = new Toggle("Recursive Search") { value = recursiveSearch };
            recursiveToggle.RegisterValueChangedCallback(e => {
                recursiveSearch = e.newValue;
                RefreshSubfolders();
            });
            settingsBox.Add(recursiveToggle);

            subfolderFoldout = new RexFoldout("Subfolders", count: null, defaultExpanded: false);
            subfolderFoldout.AddToClassList("rex-box");
            subfolderFoldout.AddToClassList("rex-hidden");
            settingsBox.Add(subfolderFoldout);

            mainScroll.Add(settingsBox);

            // --- ACTION BUTTONS ---
            var actionRow = new VisualElement();
            actionRow.AddToClassList("rex-row");
            actionRow.AddToClassList("rex-margin-top-10");
            
            runButton = new RexActionButton("FIND UNUSED ASSETS");
            runButton.AddToClassList("rex-flex-grow");
            runButton.OnClick += RunSearch;
            actionRow.Add(runButton);

            mainScroll.Add(actionRow);

            // --- TABS ---
            tabGroup = new RexTabGroup(tabs);
            tabGroup.OnTabChanged += SwitchTab;
            mainScroll.Add(tabGroup);

            // --- RESULTS AREA ---
            resultsContainer = new VisualElement();
            resultsContainer.AddToClassList("rex-box");
            resultsContainer.AddToClassList("rex-result-list");

            resultsScroll = new ScrollView();
            resultsScroll.AddToClassList("rex-flex-grow");
            resultsScroll.style.maxHeight = 300;
            resultsContainer.Add(resultsScroll);

            mainScroll.Add(resultsContainer);

            // --- DELETE SELECTED BUTTON AT THE BOTTOM ---
            deleteSelectedButton = new RexActionButton("DELETE SELECTED", tint: new Color(0.7f, 0.2f, 0.2f));
            deleteSelectedButton.style.marginTop = 10;
            deleteSelectedButton.OnClick += DeleteSelectedVisible;
            deleteSelectedButton.IsEnabled = false;
            root.Add(deleteSelectedButton);

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
                var scroll = new ScrollView();
                scroll.style.maxHeight = 200;
                DisplayFolderTree(subfolderTree, scroll);
                subfolderFoldout.Add(scroll);
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
            tabGroup?.SetSelectedTabWithoutNotify(index);
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
                var allObjects = FindObjectsByType<UnityEngine.Object>(FindObjectsSortMode.None);
                
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
                deleteSelectedButton.IsEnabled = true;
                foreach (var file in files) {
                    var item = CreateResultItem(file);
                    resultsScroll.Add(item);
                }
            } else {
                deleteSelectedButton.IsEnabled = false;
                var emptyLabel = new Label("No unused assets found in this category.");
                emptyLabel.AddToClassList("rex-empty-label");
                resultsScroll.Add(emptyLabel);
            }
        }

        private VisualElement CreateResultItem(string path)
        {
            var row = new VisualElement();
            row.AddToClassList("rex-result-item");
            // Set flex direction to ensure button and icon sit side-by-side
            row.style.flexDirection = FlexDirection.Row;

            var fileName = Path.GetFileName(path);
            var nameBtn = new Button { text = fileName };
            nameBtn.AddToClassList("rex-result-name-btn");
            nameBtn.style.flexGrow = 1; // Take up remaining space
            nameBtn.clicked += () => PingAsset(path);
            row.Add(nameBtn);

            // --- DELETE BUTTON ---
            // The text property has been removed here as well.
            var deleteBtn = new Button();
            deleteBtn.AddToClassList("rex-result-delete-btn");
            
            var icon = new VisualElement(); 
            icon.AddToClassList("rex-result-delete-icon");
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

        private void DeleteSelectedVisible()
        {
            string currentTabName = tabs[currentTabIndex];
            if (categorizedUnusedFiles.TryGetValue(currentTabName, out var files)) {
                if (EditorUtility.DisplayDialog("Delete Selected", $"Delete {files.Count} unused {currentTabName} assets?", "Yes", "No")) {
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