using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEditor.UIElements;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System;

namespace RexTools.PrefabReplacer.Editor
{
    public class PrefabReplacerWindow : EditorWindow
    {
        private List<GameObject> originals = new List<GameObject>();
        private List<GameObject> replacements = new List<GameObject>();
        
        private string searchFolder = "Assets";
        private bool matchLocal = true;
        private bool matchGlobal = false;

        private VisualElement root;
        private ScrollView originalList;
        private ScrollView replacementList;
        private VisualElement helpBox;
        private Button helpBtn;
        private TextField folderPathField;

        [MenuItem("Tools/Rex Tools/Prefab Replacer")]
        public static void ShowWindow()
        {
            var window = GetWindow<PrefabReplacerWindow>("Prefab Replacer");
            window.minSize = new Vector2(500, 600);
        }

        public void CreateGUI()
        {
            root = rootVisualElement;
            root.AddToClassList("rex-root-padding");

            // Load Global Styles
            StyleSheet styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>("Packages/com.matrixrex.rextools/Editor/RexToolsStyles.uss");
            if (styleSheet == null) styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>("Assets/Editor/RexToolsStyles.uss");
            if (styleSheet != null) root.styleSheets.Add(styleSheet);

            // --- HEADER ---
            var header = new VisualElement();
            header.AddToClassList("rex-header-row");

            var brandStack = new VisualElement();
            brandStack.AddToClassList("rex-header-stack");
            var brandLabel = new Label("Rex Tools");
            brandLabel.AddToClassList("rex-brand-label");
            brandStack.Add(brandLabel);

            var titleLabel = new Label("Prefab Replacer");
            titleLabel.AddToClassList("rex-tool-title");
            brandStack.Add(titleLabel);
            header.Add(brandStack);

            helpBtn = new Button(ToggleHelp);
            helpBtn.AddToClassList("rex-help-btn");
            header.Add(helpBtn);
            root.Add(header);

            // --- HELP BOX ---
            helpBox = new VisualElement();
            helpBox.AddToClassList("rex-help-box");
            helpBox.AddToClassList("rex-box");
            helpBox.AddToClassList("rex-hidden");
            helpBox.Add(new Label("HOW TO USE:") { style = { unityFontStyleAndWeight = FontStyle.Bold, fontSize = 10, marginBottom = 5 } });
            var h1 = new Label("• Add scene objects to the 'Original' list.");
            h1.AddToClassList("rex-help-text-item");
            helpBox.Add(h1);

            var h2 = new Label("• Add prefabs to the 'Replacement' list.");
            h2.AddToClassList("rex-help-text-item");
            helpBox.Add(h2);

            var h3 = new Label("• Use 'Find Similar' to auto-populate based on matching names.");
            h3.AddToClassList("rex-help-text-item");
            helpBox.Add(h3);

            var h4 = new Label("• Pairs are matched by longest name overlap.");
            h4.AddToClassList("rex-help-text-item");
            helpBox.Add(h4);

            var h5 = new Label("• Choose transform options and click REPLACE.");
            h5.AddToClassList("rex-help-text-item");
            helpBox.Add(h5);
            root.Add(helpBox);

            // --- FOLDER SELECTION ---
            var folderBox = new VisualElement();
            folderBox.AddToClassList("rex-box");
            var searchLabel = new Label("SEARCH SETTINGS");
            searchLabel.AddToClassList("rex-section-label");
            folderBox.Add(searchLabel);

            var folderRow = new VisualElement();
            folderRow.AddToClassList("rex-row");
            folderPathField = new TextField("Replacement Folder") { value = searchFolder, style = { flexGrow = 1 } };
            folderPathField.RegisterValueChangedCallback(evt => searchFolder = evt.newValue);
            folderRow.Add(folderPathField);

            var folderBtn = new Button(() => {
                string path = EditorUtility.OpenFolderPanel("Select Search Folder", searchFolder, "");
                if (!string.IsNullOrEmpty(path)) {
                    if (path.StartsWith(Application.dataPath)) {
                        path = "Assets" + path.Substring(Application.dataPath.Length);
                    }
                    searchFolder = path;
                    folderPathField.value = searchFolder;
                }
            }) { text = "..." };
            folderBtn.style.width = 30;
            folderRow.Add(folderBtn);
            folderBox.Add(folderRow);
            root.Add(folderBox);

            // --- MAIN CONTENT (Side by Side) ---
            var mainContent = new VisualElement();
            mainContent.style.flexDirection = FlexDirection.Row;
            mainContent.style.flexGrow = 1;

            // Column 1: Originals
            var originalCol = CreateListColumn("ORIGINALS (In Scene)", originals, true);
            originalCol.style.flexGrow = 1;
            originalCol.style.marginRight = 5;
            originalList = originalCol.Q<ScrollView>("list-scroll");
            mainContent.Add(originalCol);

            // Column 2: Replacements
            var replacementCol = CreateListColumn("REPLACEMENTS (Prefabs)", replacements, false);
            replacementCol.style.flexGrow = 1;
            replacementCol.style.marginLeft = 5;
            replacementList = replacementCol.Q<ScrollView>("list-scroll");
            mainContent.Add(replacementCol);

            root.Add(mainContent);

            // --- OPTIONS & ACTIONS ---
            var bottomBox = new VisualElement();
            bottomBox.AddToClassList("rex-box");
            bottomBox.style.marginTop = 10;
            var optionsLabel = new Label("REPLACEMENT OPTIONS");
            optionsLabel.AddToClassList("rex-section-label");
            bottomBox.Add(optionsLabel);

            var optRow = new VisualElement();
            optRow.AddToClassList("rex-row");
            var localToggle = new Toggle("Match Local Transform") { value = matchLocal };
            localToggle.RegisterValueChangedCallback(evt => { matchLocal = evt.newValue; if (matchLocal) matchGlobal = false; RefreshOptions(bottomBox); });
            optRow.Add(localToggle);

            var globalToggle = new Toggle("Match Global Transform") { value = matchGlobal };
            globalToggle.RegisterValueChangedCallback(evt => { matchGlobal = evt.newValue; if (matchGlobal) matchLocal = false; RefreshOptions(bottomBox); });
            optRow.Add(globalToggle);
            bottomBox.Add(optRow);

            var replaceBtn = new Button(ExecuteReplacement) { text = "REPLACE ALL" };
            replaceBtn.AddToClassList("rex-action-button");
            replaceBtn.AddToClassList("rex-action-button--pack");
            bottomBox.Add(replaceBtn);
            root.Add(bottomBox);

            RefreshLists();
        }

        private void RefreshOptions(VisualElement box)
        {
            box.Q<Toggle>("Match Local Transform").SetValueWithoutNotify(matchLocal);
            box.Q<Toggle>("Match Global Transform").SetValueWithoutNotify(matchGlobal);
        }

        private VisualElement CreateListColumn(string title, List<GameObject> list, bool isOriginal)
        {
            var col = new VisualElement();
            col.AddToClassList("rex-box");
            var colLabel = new Label(title);
            colLabel.AddToClassList("rex-section-label");
            col.Add(colLabel);

            var btnRow = new VisualElement();
            btnRow.AddToClassList("rex-row");
            
            var getBtn = new Button(() => {
                foreach (var go in Selection.gameObjects) {
                    if (isOriginal && go.scene.rootCount == 0) continue; // Not a scene object
                    if (!isOriginal && !EditorUtility.IsPersistent(go)) continue; // Not a prefab
                    if (!list.Contains(go)) list.Add(go);
                }
                RefreshLists();
            }) { text = "Get Selected" };
            getBtn.style.flexGrow = 1;
            btnRow.Add(getBtn);

            var findBtn = new Button(() => {
                if (isOriginal) FindSimilarOriginals();
                else FindSimilarReplacements();
            }) { text = "Find Similar" };
            findBtn.style.flexGrow = 1;
            btnRow.Add(findBtn);

            col.Add(btnRow);

            var scroll = new ScrollView();
            scroll.name = "list-scroll";
            scroll.style.height = 250;
            scroll.style.marginTop = 5;
            col.Add(scroll);

            var clearBtn = new Button(() => { list.Clear(); RefreshLists(); }) { text = "Clear" };
            clearBtn.style.marginTop = 5;
            col.Add(clearBtn);

            return col;
        }

        private void RefreshLists()
        {
            PopulateList(originalList, originals);
            PopulateList(replacementList, replacements);
        }

        private void PopulateList(ScrollView scroll, List<GameObject> list)
        {
            if (scroll == null) return;
            scroll.Clear();
            foreach (var go in list)
            {
                var row = new VisualElement();
                row.AddToClassList("rex-result-item");
                var nameLabel = new Label(go.name);
                nameLabel.AddToClassList("rex-result-name-btn");
                row.Add(nameLabel);
                
                var removeBtn = new Button(() => { list.Remove(go); RefreshLists(); });
                removeBtn.AddToClassList("rex-result-delete-btn");
                var icon = new VisualElement();
                icon.AddToClassList("rex-result-delete-icon");
                removeBtn.Add(icon);
                row.Add(removeBtn);
                
                scroll.Add(row);
            }
        }

        private void ToggleHelp()
        {
            helpBox.ToggleInClassList("rex-hidden");
            helpBtn.ToggleInClassList("rex-help-btn--active");
        }

        private void FindSimilarOriginals()
        {
            if (replacements.Count == 0)
            {
                Debug.LogWarning("[RexTools] Replacement list is empty. Add some prefabs first to find matching objects in scene.");
                return;
            }

            var sceneObjects = FindObjectsByType<GameObject>(FindObjectsSortMode.None);
            foreach (var go in sceneObjects)
            {
                if (originals.Contains(go)) continue;
                
                // If any replacement prefab name is partially matched in this scene object
                foreach (var rep in replacements)
                {
                    if (go.name.StartsWith(rep.name, StringComparison.OrdinalIgnoreCase) || 
                        rep.name.StartsWith(go.name, StringComparison.OrdinalIgnoreCase))
                    {
                        originals.Add(go);
                        break;
                    }
                }
            }
            RefreshLists();
        }

        private void FindSimilarReplacements()
        {
            if (originals.Count == 0)
            {
                Debug.LogWarning("[RexTools] Original list is empty. Add some scene objects first to find matching prefabs.");
                return;
            }

            if (!AssetDatabase.IsValidFolder(searchFolder))
            {
                Debug.LogError($"[RexTools] Invalid folder: {searchFolder}");
                return;
            }

            string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { searchFolder });
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (replacements.Contains(prefab)) continue;

                foreach (var orig in originals)
                {
                    if (prefab.name.StartsWith(orig.name, StringComparison.OrdinalIgnoreCase) || 
                        orig.name.StartsWith(prefab.name, StringComparison.OrdinalIgnoreCase))
                    {
                        replacements.Add(prefab);
                        break;
                    }
                }
            }
            RefreshLists();
        }

        private void ExecuteReplacement()
        {
            if (originals.Count == 0 || replacements.Count == 0)
            {
                Debug.LogWarning("[RexTools] Both lists must have items to perform replacement.");
                return;
            }

            Undo.SetCurrentGroupName("Prefab Replacer");
            int group = Undo.GetCurrentGroup();

            int replacedCount = 0;
            foreach (var orig in originals)
            {
                if (orig == null) continue;

                GameObject bestMatch = FindBestMatch(orig.name);
                if (bestMatch == null) continue;

                GameObject newObj = (GameObject)PrefabUtility.InstantiatePrefab(bestMatch);
                Undo.RegisterCreatedObjectUndo(newObj, "Create Replacement");

                if (matchGlobal)
                {
                    newObj.transform.position = orig.transform.position;
                    newObj.transform.rotation = orig.transform.rotation;
                    newObj.transform.localScale = orig.transform.localScale;
                    newObj.transform.SetParent(orig.transform.parent);
                }
                else
                {
                    newObj.transform.SetParent(orig.transform.parent);
                    newObj.transform.localPosition = orig.transform.localPosition;
                    newObj.transform.localRotation = orig.transform.localRotation;
                    newObj.transform.localScale = orig.transform.localScale;
                }

                Undo.DestroyObjectImmediate(orig);
                replacedCount++;
            }

            Undo.CollapseUndoOperations(group);
            Debug.Log($"[RexTools] Successfully replaced {replacedCount} objects.");
            
            originals.Clear();
            RefreshLists();
        }

        private GameObject FindBestMatch(string name)
        {
            GameObject best = null;
            int maxMatch = 0;

            foreach (var rep in replacements)
            {
                int matchLen = GetMatchLength(name, rep.name);
                if (matchLen > maxMatch)
                {
                    maxMatch = matchLen;
                    best = rep;
                }
            }
            
            // Fallback to simple startsWith/contains if no strong match found?
            // Actually the GetMatchLength should handle it.
            return best;
        }

        private int GetMatchLength(string a, string b)
        {
            a = a.ToLower();
            b = b.ToLower();
            
            // Longest Common Prefix
            int len = 0;
            int minLen = Mathf.Min(a.Length, b.Length);
            for (int i = 0; i < minLen; i++)
            {
                if (a[i] == b[i]) len++;
                else break;
            }
            
            // If one contains the other from start, that's a good match
            if (a.StartsWith(b) || b.StartsWith(a))
            {
                return Mathf.Max(len, Mathf.Min(a.Length, b.Length));
            }

            return len;
        }
    }
}
