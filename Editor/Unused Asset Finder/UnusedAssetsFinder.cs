using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
using System.IO;
using System.Linq;

public class UnusedAssetsFinder : EditorWindow
{
    private string folderPath = "Assets";
    private bool recursiveSearch = false;
    private bool showSubfolders = false;
    private bool showHelp = true;
    private List<FolderNode> subfolderTree = new List<FolderNode>();
    private int progressId = -1;

    private Dictionary<string, List<string>> categorizedUnusedFiles = new Dictionary<string, List<string>>();
    private Vector2 scrollPos;
    private string[] tabs = { "Textures", "Prefabs", "Models", "Other" };
    private int selectedTab = 0;
    private Vector2 subfolderScrollPos;

    private string lastSelectedFile = null;

    [MenuItem("PolyStream/Unused Assets Finder")]
    public static void OpenWindow()
    {
        GetWindow<UnusedAssetsFinder>("Unused Assets Finder");
    }

    private void OnGUI()
    {
        // RexTools Header
        GUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("PolyStream", new GUIStyle(EditorStyles.boldLabel));
        GUILayout.FlexibleSpace();
        if (GUILayout.Button(EditorGUIUtility.IconContent("_Help"), GUILayout.Width(24), GUILayout.Height(24)))
        {
            showHelp = !showHelp;
        }
        GUILayout.EndHorizontal();
        
       

        // Title
        EditorGUILayout.LabelField("Unused Assets Finder", new GUIStyle(EditorStyles.boldLabel)
        {
            fontSize = 16,
            alignment = TextAnchor.MiddleLeft
        });

        // Help Button
        GUILayout.Space(5);
        

        // Show Help Panel
        if (showHelp)
        {
            EditorGUILayout.HelpBox("Find unused assets from a specific folder in the currently opened scene.\n" +
                                    "- Drag and drop folders onto the path field to set the folder path.\n" +
                                    "- Click on file names to focus them in the editor.\n" +
                                    "- Use the delete button to remove unused assets.",
                                    MessageType.Info);
        }

        // Folder Path Field
        GUILayout.Space(10);
        EditorGUILayout.LabelField("Folder Path:");
        Rect folderPathRect = EditorGUILayout.GetControlRect();
        folderPath = EditorGUI.TextField(folderPathRect, folderPath);

        // Handle Drag-and-Drop
        HandleDragAndDrop(folderPathRect);

        // Recursive Search Checkbox
        recursiveSearch = EditorGUILayout.Toggle("Recursive Search", recursiveSearch);

        // Show Subfolders Section with Scroll View
        if (recursiveSearch)
        {
            GUILayout.Space(10);
            EditorGUILayout.BeginVertical("box");

            showSubfolders = EditorGUILayout.Foldout(showSubfolders, "Subfolders", true);
            if (showSubfolders)
            {
                // Add a scroll view with fixed height
                subfolderScrollPos = EditorGUILayout.BeginScrollView(subfolderScrollPos, GUILayout.Height(200));
                DisplayFolderTree(subfolderTree);
                EditorGUILayout.EndScrollView();
            }

            EditorGUILayout.EndVertical();
        }


        // Run Button
        GUI.enabled = progressId == -1;
        if (GUILayout.Button("Run", GUILayout.Height(30)))
        {
            RunUnusedAssetFinder();
        }

        // Delete All Button
        if (categorizedUnusedFiles.Any(kv => kv.Value.Count > 0))
        {
            if (GUILayout.Button("Delete All", GUILayout.Height(30)))
            {
                DeleteAllUnusedAssets();
            }
        }

        // Tabs
        GUILayout.Space(10);
        selectedTab = GUILayout.Toolbar(selectedTab, tabs);

        // Unused Files List in a Box
        GUILayout.Space(10);
        EditorGUILayout.BeginVertical("box", GUILayout.ExpandHeight(true));
        EditorGUILayout.LabelField($"Unused {tabs[selectedTab]}:", new GUIStyle(EditorStyles.boldLabel) { fontSize = 12 });

        scrollPos = EditorGUILayout.BeginScrollView(scrollPos, GUILayout.ExpandHeight(true));

        if (categorizedUnusedFiles.TryGetValue(tabs[selectedTab], out var unusedFiles))
        {
            List<string> filesToRemove = new List<string>();

            foreach (var file in unusedFiles)
            {
                string fileName = Path.GetFileName(file);

                // Wrapping Each File in a Box
                EditorGUILayout.BeginVertical("box");

                EditorGUILayout.BeginHorizontal();

                // File Name as Clickable Label with Truncation
                GUIStyle fileLabelStyle = new GUIStyle(EditorStyles.label)
                {
                    clipping = TextClipping.Clip, // Ensures truncation
                    wordWrap = false,
                    alignment = TextAnchor.MiddleLeft,
                    normal = { textColor = file == lastSelectedFile ? Color.green : EditorStyles.label.normal.textColor }
                };

                // Draw truncated file name
                if (GUILayout.Button(fileName, fileLabelStyle, GUILayout.MaxWidth(EditorGUIUtility.currentViewWidth - 100)))
                {
                    PingAsset(file);
                    lastSelectedFile = file;
                }

                // Push Delete Button to the Right
                GUILayout.FlexibleSpace();

                // Delete Icon Button
                GUIContent deleteIcon = EditorGUIUtility.IconContent("TreeEditor.Trash");
                if (GUILayout.Button(deleteIcon, GUILayout.Width(24), GUILayout.Height(24)))
                {
                    if (DeleteAsset(file))
                    {
                        filesToRemove.Add(file);
                    }
                }

                EditorGUILayout.EndHorizontal();
                EditorGUILayout.EndVertical();
            }



            foreach (var file in filesToRemove)
            {
                unusedFiles.Remove(file);
            }
        }
        else
        {
            EditorGUILayout.LabelField("No unused assets found in this category.");
        }

        EditorGUILayout.EndScrollView();
        EditorGUILayout.EndVertical();

        GUI.enabled = true;
    }

    private void HandleDragAndDrop(Rect rect)
    {
        Event evt = Event.current;

        if (evt.type == EventType.DragUpdated || evt.type == EventType.DragPerform)
        {
            if (rect.Contains(evt.mousePosition))
            {
                DragAndDrop.visualMode = DragAndDropVisualMode.Copy;

                if (evt.type == EventType.DragPerform)
                {
                    DragAndDrop.AcceptDrag();

                    if (DragAndDrop.paths.Length > 0)
                    {
                        string draggedPath = DragAndDrop.paths[0];

                        if (AssetDatabase.IsValidFolder(draggedPath))
                        {
                            folderPath = draggedPath;
                        }
                        else
                        {
                            string assetFolder = Path.GetDirectoryName(draggedPath)?.Replace("\\", "/");
                            if (!string.IsNullOrEmpty(assetFolder))
                            {
                                folderPath = assetFolder;
                            }
                        }

                        RefreshSubfolderTree();
                        evt.Use();
                    }
                }
            }
        }
    }

    private void RunUnusedAssetFinder()
    {
        categorizedUnusedFiles.Clear();
        if (!Directory.Exists(folderPath))
        {
            Debug.LogError("Folder path does not exist.");
            return;
        }

        progressId = Progress.Start("Finding Unused Assets", "Processing...", Progress.Options.Indefinite);

        EditorApplication.update += ProcessAssetsInBackground;
    }

    private void ProcessAssetsInBackground()
    {
        try
        {
            if (categorizedUnusedFiles.Count == 0)
            {
                var files = Directory.GetFiles(folderPath, "*.*", recursiveSearch ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly)
                                     .Where(file => !file.EndsWith(".meta"))
                                     .ToList();

                Progress.Report(progressId, 0, "Gathering scene references...");

                var sceneReferences = new HashSet<string>();
                var allObjects = FindObjectsOfType<UnityEngine.Object>();
                foreach (var obj in allObjects)
                {
                    var dependencies = EditorUtility.CollectDependencies(new[] { obj });
                    foreach (var dependency in dependencies)
                    {
                        var path = AssetDatabase.GetAssetPath(dependency);
                        if (!string.IsNullOrEmpty(path) && !sceneReferences.Contains(path))
                        {
                            sceneReferences.Add(path);
                        }
                    }
                }

                Progress.Report(progressId, 0.5f, "Categorizing assets...");

                foreach (var file in files)
                {
                    var relativePath = file.Replace(Application.dataPath, "Assets").Replace("\\", "/");
                    if (!sceneReferences.Contains(relativePath))
                    {
                        CategorizeFile(relativePath);
                    }
                }
            }
        }
        finally
        {
            Progress.Finish(progressId);
            progressId = -1;
            EditorApplication.update -= ProcessAssetsInBackground;
            Repaint();
        }
    }

    private void CategorizeFile(string filePath)
    {
        string extension = Path.GetExtension(filePath).ToLower();

        string category = extension switch
        {
            ".png" or ".jpg" or ".jpeg" or ".tga" or ".bmp" => "Textures",
            ".prefab" => "Prefabs",
            ".fbx" or ".obj" or ".blend" => "Models",
            _ => "Other",
        };

        if (!categorizedUnusedFiles.ContainsKey(category))
        {
            categorizedUnusedFiles[category] = new List<string>();
        }

        categorizedUnusedFiles[category].Add(filePath);
    }

    private void RefreshSubfolderTree()
    {
        subfolderTree.Clear();

        if (Directory.Exists(folderPath))
        {
            subfolderTree = BuildFolderTree(folderPath);
        }
    }

    private List<FolderNode> BuildFolderTree(string rootPath)
    {
        List<FolderNode> tree = new List<FolderNode>();
        foreach (string dir in Directory.GetDirectories(rootPath))
        {
            var node = new FolderNode
            {
                Name = Path.GetFileName(dir),
                Subfolders = BuildFolderTree(dir)
            };
            tree.Add(node);
        }
        return tree;
    }

    private void DisplayFolderTree(List<FolderNode> nodes, int indent = 0)
    {
        foreach (var node in nodes)
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(indent * 15);
            EditorGUILayout.LabelField($"- {node.Name}");
            EditorGUILayout.EndHorizontal();

            if (node.Subfolders.Count > 0)
            {
                DisplayFolderTree(node.Subfolders, indent + 1);
            }
        }
    }

    private void PingAsset(string path)
    {
        var asset = AssetDatabase.LoadAssetAtPath<Object>(path);
        if (asset != null)
        {
            EditorGUIUtility.PingObject(asset);
        }
    }

    private bool DeleteAsset(string path)
    {
        if (EditorUtility.DisplayDialog("Delete Asset", $"Are you sure you want to delete {path}?", "Yes", "No"))
        {
            AssetDatabase.DeleteAsset(path);
            Debug.Log($"Deleted asset: {path}");
            return true;
        }

        return false;
    }

    private void DeleteAllUnusedAssets()
    {
        if (EditorUtility.DisplayDialog("Delete All Unused Assets", $"Are you sure you want to delete all unused assets?", "Yes", "No"))
        {
            foreach (var kv in categorizedUnusedFiles)
            {
                foreach (var file in kv.Value.ToList())
                {
                    AssetDatabase.DeleteAsset(file);
                }
            }

            categorizedUnusedFiles.Clear();
            Debug.Log("Deleted all unused assets.");
        }
    }

    private class FolderNode
    {
        public string Name;
        public List<FolderNode> Subfolders = new List<FolderNode>();
    }
}
