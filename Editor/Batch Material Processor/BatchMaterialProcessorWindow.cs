using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using RexTools.Editor.Core;

namespace RexTools.BatchMaterialProcessor.Editor
{
    public class BatchMaterialProcessorWindow : EditorWindow
    {
        private BatchMaterialProcessorSettings settings;
        
        private RexHelpBox helpBox;
        private bool showHelp = false;
        
        private ScrollView materialsScroll;
        private ObjectField shaderField;
        private RexFolderSelector folderSelector;
        private Toggle recursiveToggle;
        
        private Button tabSuffixes;
        private Button tabPreview;
        private VisualElement paneSuffixes;
        private VisualElement panePreview;
        
        private ScrollView suffixesScroll;
        private ScrollView previewScroll;
        
        private Button btnProcess;
        private Button btnApply;

        [MenuItem("Tools/Rex Tools/Batch Material Processor")]
        public static void ShowWindow()
        {
            var wnd = GetWindow<BatchMaterialProcessorWindow>();
            wnd.titleContent = new GUIContent("Batch Material Processor");
            wnd.minSize = new Vector2(400, 600);
        }

        public void CreateGUI()
        {
            if (settings == null)
            {
                settings = ScriptableObject.CreateInstance<BatchMaterialProcessorSettings>();
                settings.name = "BatchMaterialProcessorSettings_Runtime";
            }
            if (settings.materials == null) settings.materials = new List<Material>();
            if (settings.suffixMappings == null) settings.suffixMappings = new List<SuffixMapping>();
            if (settings.matchResults == null) settings.matchResults = new List<MaterialMatchResult>();

            VisualElement root = rootVisualElement;
            
            // Load UXML
            string windowPath = "Editor/Batch Material Processor/BatchMaterialProcessorWindow.uxml";
            string fullPath = "Assets/" + windowPath;
            if (AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(fullPath) == null)
            {
                fullPath = "Packages/com.matrixrex.rextools/" + windowPath;
            }

            // Force Unity to import the asset so that it isn't empty or stale in the AssetDatabase
            AssetDatabase.ImportAsset(fullPath);

            var visualTree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(fullPath);
            if (visualTree != null)
            {
                visualTree.CloneTree(root);
            }
            else
            {
                root.Add(new Label("Could not load BatchMaterialProcessorWindow.uxml"));
                return;
            }

            // Load Stylesheet
            string[] possibleStyles = {
                "Packages/com.matrixrex.rextools/Editor/RexToolsStyles.uss",
                "Assets/Editor/RexToolsStyles.uss"
            };
            StyleSheet styleSheet = null;
            foreach (var path in possibleStyles)
            {
                styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(path);
                if (styleSheet != null) break;
            }
            if (styleSheet != null) root.styleSheets.Add(styleSheet);

            // Bind UI Elements
            // --- BRANDED HEADER & HELP BOX ---
            var helpBoxContainer = root.Q<VisualElement>("help-box-container");
            if (helpBoxContainer != null)
            {
                helpBox = new RexHelpBox(
                    "Add materials using selection or drag-and-drop.",
                    "Set a target Shader and search folder path.",
                    "Edit suffix rules under Suffixes tab.",
                    "Click PROCESS MATCHES to dry-run, then check the Preview tab.",
                    "Click APPLY to convert materials and assign textures."
                );
                helpBoxContainer.Add(helpBox);
            }

            var headerContainer = root.Q<VisualElement>("header-container");
            if (headerContainer != null)
            {
                var header = new RexHeader("Batch Material Processor", showHelpButton: true);
                header.OnHelpClicked += () => {
                    showHelp = !showHelp;
                    helpBox?.ToggleVisibility();
                    header.SetHelpButtonActive(showHelp);
                };
                headerContainer.Add(header);
            }

            // Preset Setup
            var presetContainer = root.Q<VisualElement>("preset-btn-container");
            if (presetContainer != null)
            {
                var presetBtn = RexPresetManager.CreatePresetIconButton(settings);
                presetContainer.Add(presetBtn);
            }

            // Material list
            materialsScroll = root.Q<ScrollView>("materials-scroll");
            var btnGetSelection = root.Q<Button>("btn-get-selection");
            if (btnGetSelection != null) btnGetSelection.clicked += GetSelection;
            var btnClearMats = root.Q<Button>("btn-clear-mats");
            if (btnClearMats != null)
            {
                btnClearMats.clicked += () => {
                    settings.materials.Clear();
                    settings.matchResults.Clear();
                    EditorUtility.SetDirty(settings);
                    RefreshMaterialsUI();
                    RefreshPreviewTab();
                };
            }

            // Drag and drop to materials list
            if (materialsScroll != null)
            {
                materialsScroll.RegisterCallback<DragUpdatedEvent>(e => DragAndDrop.visualMode = DragAndDropVisualMode.Copy);
                materialsScroll.RegisterCallback<DragPerformEvent>(e => {
                    DragAndDrop.AcceptDrag();
                    foreach (var obj in DragAndDrop.objectReferences)
                    {
                        if (obj is Material mat && !settings.materials.Contains(mat))
                        {
                            settings.materials.Add(mat);
                        }
                        else if (obj is GameObject go)
                        {
                            var renderers = go.GetComponentsInChildren<Renderer>(true);
                            foreach (var r in renderers)
                            {
                                foreach (var sm in r.sharedMaterials)
                                {
                                    if (sm != null && !settings.materials.Contains(sm))
                                        settings.materials.Add(sm);
                                }
                            }
                        }
                    }
                    EditorUtility.SetDirty(settings);
                    RefreshMaterialsUI();
                });
            }

            // Settings properties
            shaderField = root.Q<ObjectField>("shader-field");
            if (shaderField != null)
            {
                shaderField.objectType = typeof(Shader);
                shaderField.value = settings.targetShader;
                shaderField.RegisterValueChangedCallback(evt => {
                    settings.targetShader = evt.newValue as Shader;
                    EditorUtility.SetDirty(settings);
                    LoadSuffixMappings();
                });
            }

            var selectorContainer = root.Q<VisualElement>("folder-selector-container");
            if (selectorContainer != null)
            {
                folderSelector = new RexFolderSelector();
                folderSelector.SetPathWithoutNotify(settings.searchFolderPath);
                folderSelector.OnValueChanged += path => {
                    settings.searchFolderPath = path;
                    EditorUtility.SetDirty(settings);
                };
                selectorContainer.Add(folderSelector);
            }

            recursiveToggle = root.Q<Toggle>("recursive-toggle");
            if (recursiveToggle != null)
            {
                recursiveToggle.value = settings.recursiveSearch;
                recursiveToggle.RegisterValueChangedCallback(evt => {
                    settings.recursiveSearch = evt.newValue;
                    EditorUtility.SetDirty(settings);
                });
            }

            // Tabs
            tabSuffixes = root.Q<Button>("tab-suffixes");
            tabPreview = root.Q<Button>("tab-preview");
            paneSuffixes = root.Q<VisualElement>("pane-suffixes");
            panePreview = root.Q<VisualElement>("pane-preview");

            if (tabSuffixes != null) tabSuffixes.clicked += () => SwitchTab(true);
            if (tabPreview != null) tabPreview.clicked += () => SwitchTab(false);

            suffixesScroll = root.Q<ScrollView>("suffixes-scroll");
            previewScroll = root.Q<ScrollView>("preview-scroll");

            btnProcess = root.Q<Button>("btn-process");
            if (btnProcess != null) btnProcess.clicked += RunProcess;

            btnApply = root.Q<Button>("btn-apply");
            if (btnApply != null)
            {
                btnApply.clicked += ApplyChanges;
                btnApply.SetEnabled(settings.matchResults.Count > 0);
            }

            // Initial Draws
            RefreshMaterialsUI();
            if (settings.targetShader != null && settings.suffixMappings.Count == 0)
            {
                LoadSuffixMappings();
            }
            else
            {
                RefreshSuffixMappingsUI();
            }
            RefreshPreviewTab();
        }

        private void OnInspectorUpdate()
        {
            if (settings != null)
            {
                if (settings.materials == null) settings.materials = new List<Material>();
                if (settings.suffixMappings == null) settings.suffixMappings = new List<SuffixMapping>();
                if (settings.matchResults == null) settings.matchResults = new List<MaterialMatchResult>();

                // Sync values if settings changed via Preset selection
                if (shaderField != null && shaderField.value != settings.targetShader)
                {
                    shaderField.value = settings.targetShader;
                    LoadSuffixMappings();
                }
                if (folderSelector != null && folderSelector.PathValue != settings.searchFolderPath)
                {
                    folderSelector.SetPathWithoutNotify(settings.searchFolderPath);
                }
                if (recursiveToggle != null && recursiveToggle.value != settings.recursiveSearch)
                {
                    recursiveToggle.value = settings.recursiveSearch;
                }
            }
        }

        private void SwitchTab(bool toSuffixes)
        {
            paneSuffixes.style.display = toSuffixes ? DisplayStyle.Flex : DisplayStyle.None;
            panePreview.style.display = toSuffixes ? DisplayStyle.None : DisplayStyle.Flex;

            tabSuffixes.RemoveFromClassList("rex-tab-button--active");
            tabSuffixes.RemoveFromClassList("rex-tab-button--inactive");
            tabSuffixes.AddToClassList(toSuffixes ? "rex-tab-button--active" : "rex-tab-button--inactive");

            tabPreview.RemoveFromClassList("rex-tab-button--active");
            tabPreview.RemoveFromClassList("rex-tab-button--inactive");
            tabPreview.AddToClassList(toSuffixes ? "rex-tab-button--inactive" : "rex-tab-button--active");
        }

        private void GetSelection()
        {
            foreach (var obj in Selection.objects)
            {
                if (obj is Material mat && !settings.materials.Contains(mat))
                {
                    settings.materials.Add(mat);
                }
            }

            foreach (var go in Selection.gameObjects)
            {
                var renderers = go.GetComponentsInChildren<Renderer>(true);
                foreach (var r in renderers)
                {
                    foreach (var sm in r.sharedMaterials)
                    {
                        if (sm != null && !settings.materials.Contains(sm))
                            settings.materials.Add(sm);
                    }
                }
            }

            EditorUtility.SetDirty(settings);
            RefreshMaterialsUI();
        }

        private void RefreshMaterialsUI()
        {
            if (materialsScroll == null) return;
            if (settings == null || settings.materials == null) return;

            materialsScroll.Clear();
            if (settings.materials.Count == 0)
            {
                var label = new Label("Drag materials here or click GET FROM SELECTION");
                label.style.color = new Color(0.5f, 0.5f, 0.5f);
                label.style.fontSize = 10f;
                label.style.unityTextAlign = TextAnchor.MiddleCenter;
                label.style.marginTop = 10f;
                materialsScroll.Add(label);
                return;
            }

            for (int i = 0; i < settings.materials.Count; i++)
            {
                var mat = settings.materials[i];
                if (mat == null) continue;

                var row = new VisualElement();
                row.AddToClassList("rex-result-item");

                var matLabel = new Label(mat.name);
                matLabel.style.fontSize = 10f;
                matLabel.style.flexGrow = 1;
                matLabel.style.flexShrink = 1;
                matLabel.style.minWidth = 0;
                row.Add(matLabel);

                var deleteBtn = new Button(() => {
                    settings.materials.Remove(mat);
                    settings.matchResults.RemoveAll(r => r.material == mat);
                    EditorUtility.SetDirty(settings);
                    RefreshMaterialsUI();
                    RefreshPreviewTab();
                });
                deleteBtn.AddToClassList("rex-result-delete-btn");
                deleteBtn.tooltip = "Remove from list";

                var trashIcon = new VisualElement();
                trashIcon.AddToClassList("rex-result-delete-icon");
                deleteBtn.Add(trashIcon);
                row.Add(deleteBtn);

                materialsScroll.Add(row);
            }
        }

        private void LoadSuffixMappings()
        {
            settings.suffixMappings.Clear();
            if (settings.targetShader == null)
            {
                RefreshSuffixMappingsUI();
                return;
            }

            int propCount = ShaderUtil.GetPropertyCount(settings.targetShader);
            for (int i = 0; i < propCount; i++)
            {
                if (ShaderUtil.IsShaderPropertyHidden(settings.targetShader, i)) continue;

                var type = ShaderUtil.GetPropertyType(settings.targetShader, i);
                if (type != ShaderUtil.ShaderPropertyType.TexEnv) continue;

                string name = ShaderUtil.GetPropertyName(settings.targetShader, i);
                string desc = ShaderUtil.GetPropertyDescription(settings.targetShader, i);

                string defaultSuffixes = "";
                string normName = name.ToLowerInvariant();
                string normDesc = desc.ToLowerInvariant();

                if (normName.Contains("albedo") || normName.Contains("basecolor") || normName.Contains("maintex") || normName.Contains("color") || normName.Contains("diffuse") ||
                    normDesc.Contains("albedo") || normDesc.Contains("base color") || normDesc.Contains("color") || normDesc.Contains("diffuse"))
                {
                    defaultSuffixes = "_albedo, _basecolor, _diffuse, _color, _alb, _d, albedotransparency";
                }
                else if (normName.Contains("bump") || normName.Contains("normal") || normDesc.Contains("normal") || normDesc.Contains("bump"))
                {
                    defaultSuffixes = "_normal, _n, _bump, _norm";
                }
                else if (normName.Contains("metallic") || normDesc.Contains("metallic"))
                {
                    defaultSuffixes = "_metallic, _metal, _m, _met";
                }
                else if (normName.Contains("roughness") || normDesc.Contains("roughness"))
                {
                    defaultSuffixes = "_roughness, _rough, _r, _rog";
                }
                else if (normName.Contains("occlusion") || normName.Contains("ao") || normDesc.Contains("occlusion") || normDesc.Contains("ao"))
                {
                    defaultSuffixes = "_occlusion, _ao, _occ, _ambientocclusion";
                }
                else if (normName.Contains("emission") || normDesc.Contains("emission") || normDesc.Contains("emit"))
                {
                    defaultSuffixes = "_emission, _emit, _e";
                }
                else if (normName.Contains("height") || normName.Contains("parallax") || normDesc.Contains("height") || normDesc.Contains("displacement") || normDesc.Contains("parallax"))
                {
                    defaultSuffixes = "_height, _h, _displacement, _disp, _parallax";
                }
                else
                {
                    defaultSuffixes = "_" + name.TrimStart('_').ToLowerInvariant();
                }

                settings.suffixMappings.Add(new SuffixMapping
                {
                    propertyName = name,
                    propertyDescription = desc,
                    suffixes = defaultSuffixes
                });
            }
            EditorUtility.SetDirty(settings);
            RefreshSuffixMappingsUI();
        }

        private void RefreshSuffixMappingsUI()
        {
            if (suffixesScroll == null) return;
            if (settings == null || settings.suffixMappings == null) return;

            suffixesScroll.Clear();
            if (settings.suffixMappings.Count == 0)
            {
                var label = new Label("Select a Shader with texture properties to show suffix rules.");
                label.AddToClassList("rex-empty-label");
                suffixesScroll.Add(label);
                return;
            }

            foreach (var mapping in settings.suffixMappings)
            {
                var row = new VisualElement();
                row.AddToClassList("rex-row-cols-2");
                row.style.marginBottom = 6;

                var label = new Label($"{mapping.propertyDescription} ({mapping.propertyName})");
                label.AddToClassList("rex-col-left");
                label.style.fontSize = 10f;
                row.Add(label);

                var txt = new TextField();
                txt.AddToClassList("rex-col-right");
                txt.value = mapping.suffixes;
                txt.RegisterValueChangedCallback(evt => {
                    mapping.suffixes = evt.newValue;
                    EditorUtility.SetDirty(settings);
                });
                row.Add(txt);

                suffixesScroll.Add(row);
            }
        }

        private string Normalize(string input)
        {
            if (string.IsNullOrEmpty(input)) return "";
            return input.ToLowerInvariant()
                        .Replace(" ", "")
                        .Replace("_", "")
                        .Replace("-", "");
        }

        private void RunProcess()
        {
            if (settings.targetShader == null)
            {
                EditorUtility.DisplayDialog("Error", "Please assign a target shader.", "OK");
                return;
            }

            if (string.IsNullOrEmpty(settings.searchFolderPath) || !Directory.Exists(settings.searchFolderPath))
            {
                EditorUtility.DisplayDialog("Error", "Please specify a valid texture search folder.", "OK");
                return;
            }

            var searchOption = settings.recursiveSearch ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
            var textureExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".png", ".jpg", ".jpeg", ".tga", ".bmp", ".psd", ".tif", ".tiff" };

            var allTexturePaths = Directory.GetFiles(settings.searchFolderPath, "*.*", searchOption)
                .Where(f => textureExtensions.Contains(Path.GetExtension(f)))
                .Select(f => f.Replace("\\", "/"))
                .ToList();

            settings.matchResults.Clear();

            foreach (var mat in settings.materials)
            {
                if (mat == null) continue;

                var result = new MaterialMatchResult { material = mat };
                string normMatName = Normalize(mat.name);

                foreach (var mapping in settings.suffixMappings)
                {
                    var entry = new PropertyMatchEntry
                      {
                          propertyName = mapping.propertyName,
                          propertyDescription = mapping.propertyDescription,
                          isSelected = true
                      };

                    var suffixes = mapping.suffixes.Split(',')
                        .Select(s => s.Trim())
                        .Where(s => !string.IsNullOrEmpty(s))
                        .Select(s => Normalize(s))
                        .ToList();

                    string bestMatchPath = null;
                    int bestScore = 0;
                    int bestLen = int.MaxValue;

                    foreach (var texPath in allTexturePaths)
                    {
                        string texFilename = Path.GetFileNameWithoutExtension(texPath);
                        string normTexName = Normalize(texFilename);

                        if (!normTexName.Contains(normMatName)) continue;

                        foreach (var suffix in suffixes)
                        {
                            if (normTexName.Contains(suffix))
                            {
                                int score = 1;
                                if (normTexName.EndsWith(suffix))
                                {
                                    score = normTexName.StartsWith(normMatName) ? 3 : 2;
                                }

                                if (score > bestScore)
                                {
                                    bestScore = score;
                                    bestMatchPath = texPath;
                                    bestLen = texFilename.Length;
                                }
                                else if (score == bestScore && texFilename.Length < bestLen)
                                {
                                    bestMatchPath = texPath;
                                    bestLen = texFilename.Length;
                                }
                            }
                        }
                    }

                    if (!string.IsNullOrEmpty(bestMatchPath))
                    {
                        entry.matchedTexture = AssetDatabase.LoadAssetAtPath<Texture>(bestMatchPath);
                    }

                    result.propertyMatches.Add(entry);
                }

                settings.matchResults.Add(result);
            }

            EditorUtility.SetDirty(settings);
            btnApply.SetEnabled(settings.matchResults.Count > 0);
            SwitchTab(false); // Switch to preview tab
            RefreshPreviewTab();
        }

        private void RefreshPreviewTab()
        {
            if (previewScroll == null) return;
            if (settings == null || settings.matchResults == null) return;

            previewScroll.Clear();
            if (settings.matchResults.Count == 0)
            {
                var label = new Label("No matches processed. Configure settings and click PROCESS MATCHES.");
                label.AddToClassList("rex-empty-label");
                previewScroll.Add(label);
                return;
            }

            foreach (var result in settings.matchResults)
            {
                if (result.material == null) continue;

                var foldout = new RexFoldout(result.material.name, result.propertyMatches.Count, result.isExpanded);
                foldout.OnValueChanged += (expanded) => {
                    result.isExpanded = expanded;
                    EditorUtility.SetDirty(settings);
                };

                foreach (var match in result.propertyMatches)
                {
                    var row = new VisualElement();
                    row.AddToClassList("rex-row-cols-2");
                    row.style.marginBottom = 4;

                    var leftCol = new VisualElement();
                    leftCol.AddToClassList("rex-col-left");
                    leftCol.style.flexDirection = FlexDirection.Row;
                    leftCol.style.alignItems = Align.Center;

                    var toggle = new Toggle();
                    toggle.value = match.isSelected;
                    toggle.style.marginRight = 4;
                    toggle.RegisterValueChangedCallback(evt => {
                        match.isSelected = evt.newValue;
                        EditorUtility.SetDirty(settings);
                    });
                    leftCol.Add(toggle);

                    var propLabel = new Label($"{match.propertyDescription} ({match.propertyName})");
                    propLabel.style.fontSize = 9f;
                    propLabel.style.flexGrow = 1;
                    propLabel.style.flexShrink = 1;
                    propLabel.style.minWidth = 0;
                    leftCol.Add(propLabel);
                    
                    row.Add(leftCol);

                    var texField = new ObjectField();
                    texField.AddToClassList("rex-col-right");
                    texField.objectType = typeof(Texture);
                    texField.value = match.overrideTexture != null ? match.overrideTexture : match.matchedTexture;
                    texField.RegisterValueChangedCallback(evt => {
                        match.overrideTexture = evt.newValue as Texture;
                        EditorUtility.SetDirty(settings);
                    });
                    row.Add(texField);

                    foldout.Add(row);
                }

                previewScroll.Add(foldout);
            }
        }

        private void ApplyChanges()
        {
            if (settings.matchResults.Count == 0)
            {
                EditorUtility.DisplayDialog("Error", "Please process matches first.", "OK");
                return;
            }

            if (!EditorUtility.DisplayDialog("Confirm Apply", $"Apply shader '{settings.targetShader.name}' and matched textures to {settings.matchResults.Count} materials?", "Apply", "Cancel"))
            {
                return;
            }

            Undo.RecordObjects(settings.matchResults.Select(r => r.material).ToArray(), "Batch Material Texture Processor");

            int appliedCount = 0;
            foreach (var result in settings.matchResults)
            {
                if (result.material == null) continue;

                result.material.shader = settings.targetShader;

                foreach (var match in result.propertyMatches)
                {
                    if (!match.isSelected) continue;

                    Texture texToAssign = match.overrideTexture != null ? match.overrideTexture : match.matchedTexture;
                    if (texToAssign != null)
                    {
                        result.material.SetTexture(match.propertyName, texToAssign);
                    }
                }
                EditorUtility.SetDirty(result.material);
                appliedCount++;
            }

            AssetDatabase.SaveAssets();
            EditorUtility.DisplayDialog("Success", $"Successfully updated {appliedCount} materials.", "OK");
        }
    }
}
