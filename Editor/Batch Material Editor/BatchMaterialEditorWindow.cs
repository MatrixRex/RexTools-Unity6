using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using System.IO;
using System.Linq;
using System;
using RexTools.BatchMaterialEditor;
using RexTools.Editor.Core;

namespace RexTools.BatchMaterialEditor.Editor
{
    public class BatchMaterialEditorWindow : EditorWindow
    {
        public enum MatPropType { Texture, Color, Float, Vector }

        [System.Serializable]
        public class MaterialEntry
        {
            public Material material;
            public string propertyName = "_BaseColor";
        }

        [System.Serializable]
        public class PropertyGroup
        {
            public string groupName = "New Group";
            public MatPropType propertyType = MatPropType.Color;
            public List<MaterialEntry> materials = new List<MaterialEntry>();

            public Color colorVal = Color.white;
            public float floatVal = 0f;
            public Vector4 vectorVal = Vector4.zero;
            public Texture textureVal = null;
            public bool isExpanded = true;

            public PropertyGroup Clone()
            {
                var clone = new PropertyGroup
                {
                    groupName = this.groupName + " (Copy)",
                    propertyType = this.propertyType,
                    colorVal = this.colorVal,
                    floatVal = this.floatVal,
                    vectorVal = this.vectorVal,
                    textureVal = this.textureVal,
                    isExpanded = this.isExpanded
                };
                foreach (var matEntry in this.materials)
                {
                    clone.materials.Add(new MaterialEntry { material = matEntry.material, propertyName = matEntry.propertyName });
                }
                return clone;
            }
        }

        [SerializeField]
        private List<PropertyGroup> propertyGroups = new List<PropertyGroup>();

        // Selected materials for creating new group
        private HashSet<Material> selectedMaterials = new HashSet<Material>();
        private Button btnCreateGroupFromSelection;

        // UI Elements
        private VisualElement scannerContainer;
        private VisualElement editorContainer;
        private VisualElement replaceContainer;
        private VisualElement switcherContainer;

        private ScrollView scannerList;
        private ScrollView groupsList;
        private ScrollView affectedListView;
        private ScrollView switcherMappingList;

        private List<Button> tabButtons = new List<Button>();

        // Find/Replace Data
        private Material findMat;
        private Material replaceMat;
        public enum ReplaceMode { Scene, Prefab, NewPrefab }
        private ReplaceMode replaceMode = ReplaceMode.Scene;
        private List<UnityEngine.Object> affectedObjects = new List<UnityEngine.Object>();
        private Button btnConvert;

        // Switcher Data
        private Shader sourceShader;
        private Shader targetShader;
        private Material sourcePreviewMat;
        private Material targetPreviewMat;

        private enum ConvPropertyType { Float, Color, Vector, Texture, Unknown }
        private class ConvPropertyMapping
        {
            public string sourcePropName;
            public ConvPropertyType type;
            public string targetPropName = "None";
            public string[] targetOptions = new string[] { "None" };
            public int selectedIndex = 0;
            public bool isValid = false;
        }
        private List<ConvPropertyMapping> convMappings = new List<ConvPropertyMapping>();

        // New Preset-based settings
        [SerializeField]
        private MaterialConverterPreset activeSwitcherSettings;
        private SerializedObject serializedSwitcherSettings;

        [MenuItem("Tools/Rex Tools/Batch Material Editor")]
        public static void ShowWindow()
        {
            BatchMaterialEditorWindow wnd = GetWindow<BatchMaterialEditorWindow>();
            wnd.titleContent = new GUIContent("Batch Material Editor");
            wnd.minSize = new Vector2(450, 600);
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

            var titleLabel = new Label("Batch Material Editor");
            titleLabel.AddToClassList("rex-tool-title");
            brandStack.Add(titleLabel);

            header.Add(brandStack);
            root.Add(header);

            // --- TABS ---
            var tabsContainer = new VisualElement();
            tabsContainer.AddToClassList("rex-tabs-container");

            string[] tabNames = { "Scanner", "Editor", "Replace", "Switcher" };
            for (int i = 0; i < tabNames.Length; i++)
            {
                int index = i;
                var btn = new Button(() => SwitchTab(index)) { text = tabNames[i] };
                btn.AddToClassList("rex-tab-button");
                tabButtons.Add(btn);
                tabsContainer.Add(btn);
            }
            root.Add(tabsContainer);

            // Init Settings
            if (activeSwitcherSettings == null)
            {
                activeSwitcherSettings = ScriptableObject.CreateInstance<MaterialConverterPreset>();
                activeSwitcherSettings.hideFlags = HideFlags.HideAndDontSave;
            }
            serializedSwitcherSettings = new SerializedObject(activeSwitcherSettings);

            // --- TAB CONTAINERS ---
            CreateScannerUI(root);
            CreateEditorUI(root);
            CreateReplaceUI(root);
            CreateSwitcherUI(root);

            SwitchTab(0);
            RefreshGroupsUI();
        }

        private void OnInspectorUpdate()
        {
            if (activeSwitcherSettings != null && (activeSwitcherSettings.sourceShader != sourceShader || activeSwitcherSettings.targetShader != targetShader))
            {
                sourceShader = activeSwitcherSettings.sourceShader;
                targetShader = activeSwitcherSettings.targetShader;
                sourcePreviewMat = activeSwitcherSettings.sourcePreviewMat;
                targetPreviewMat = activeSwitcherSettings.targetPreviewMat;
                
                // If shaders changed, we likely need to reload mappings
                LoadMappings();
                SyncSettingsToMappings();
                rootVisualElement.Query<ObjectField>("Source Shader").First().value = sourceShader;
                rootVisualElement.Query<ObjectField>("Target Shader").First().value = targetShader;
            }
        }

        private void SyncSettingsToMappings()
        {
            if (activeSwitcherSettings == null || activeSwitcherSettings.propertyPairs.Count == 0) return;
            
            foreach (var pair in activeSwitcherSettings.propertyPairs)
            {
                var m = convMappings.FirstOrDefault(x => x.sourcePropName == pair.sourceProperty);
                if (m != null)
                {
                    m.targetPropName = pair.targetProperty;
                    m.selectedIndex = pair.selectedIndex;
                    m.isValid = m.targetPropName != "None";
                }
            }
            RefreshMappingsUI();
        }

        private void SwitchTab(int index)
        {
            scannerContainer.style.display = index == 0 ? DisplayStyle.Flex : DisplayStyle.None;
            editorContainer.style.display = index == 1 ? DisplayStyle.Flex : DisplayStyle.None;
            replaceContainer.style.display = index == 2 ? DisplayStyle.Flex : DisplayStyle.None;
            switcherContainer.style.display = index == 3 ? DisplayStyle.Flex : DisplayStyle.None;

            for (int i = 0; i < tabButtons.Count; i++)
            {
                tabButtons[i].RemoveFromClassList("rex-tab-button--active");
                tabButtons[i].RemoveFromClassList("rex-tab-button--inactive");
                tabButtons[i].AddToClassList(i == index ? "rex-tab-button--active" : "rex-tab-button--inactive");
            }
        }

        #region Scanner Logic

        private void CreateScannerUI(VisualElement root)
        {
            scannerContainer = new VisualElement { style = { flexGrow = 1 } };
            
            var btnScan = new Button(PerformScan) { text = "SCAN SCENE MATERIALS" };
            btnScan.AddToClassList("rex-action-button");
            btnScan.AddToClassList("rex-action-button--pack");
            scannerContainer.Add(btnScan);

            btnCreateGroupFromSelection = new Button(CreateGroupFromSelection)
            {
                text = "CREATE GROUP FROM SELECTION",
                style = { display = DisplayStyle.None }
            };
            btnCreateGroupFromSelection.AddToClassList("rex-action-button");
            btnCreateGroupFromSelection.AddToClassList("rex-action-button--unpack");
            scannerContainer.Add(btnCreateGroupFromSelection);

            scannerList = new ScrollView { style = { flexGrow = 1 } };
            scannerList.AddToClassList("rex-box");
            scannerList.AddToClassList("rex-result-list");
            scannerContainer.Add(scannerList);

            root.Add(scannerContainer);
        }

        private void PerformScan()
        {
            scannerList.Clear();
            selectedMaterials.Clear();
            UpdateCreateGroupButtonVisibility();

            HashSet<Material> foundMaterials = new HashSet<Material>();
            var renderers = FindObjectsByType<Renderer>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var r in renderers)
            {
                if (r.sharedMaterials == null) continue;
                foreach (var mat in r.sharedMaterials) if (mat != null) foundMaterials.Add(mat);
            }

            if (foundMaterials.Count == 0)
            {
                scannerList.Add(new Label("No materials found in current scenes.") { style = { marginTop = 20, alignSelf = Align.Center } });
                return;
            }

            foreach (var mat in foundMaterials)
            {
                var row = new VisualElement();
                row.AddToClassList("rex-result-item");

                var toggle = new Toggle { value = false, style = { marginRight = 5 } };
                toggle.RegisterValueChangedCallback(evt => {
                    if (evt.newValue) selectedMaterials.Add(mat);
                    else selectedMaterials.Remove(mat);
                    UpdateCreateGroupButtonVisibility();
                });
                row.Add(toggle);

                var matField = new ObjectField { objectType = typeof(Material), value = mat, style = { flexGrow = 1, flexShrink = 1, minWidth = 0 } };
                matField.SetEnabled(false);

                var focusBtn = new Button(() => { EditorGUIUtility.PingObject(mat); }) { tooltip = "Focus in Project" };
                focusBtn.AddToClassList("rex-button-small");
                var searchIcon = EditorGUIUtility.IconContent("d_ViewToolZoom").image;
                if (searchIcon != null)
                {
                    var icon = new VisualElement();
                    icon.style.backgroundImage = (Texture2D)searchIcon;
                    icon.style.width = 16;
                    icon.style.height = 16;
                    focusBtn.Add(icon);
                }
                else focusBtn.text = "F";

                row.Add(matField);
                row.Add(focusBtn);
                scannerList.Add(row);
            }
        }

        private void UpdateCreateGroupButtonVisibility()
        {
            if (btnCreateGroupFromSelection != null)
            {
                btnCreateGroupFromSelection.style.display = selectedMaterials.Count > 0 ? DisplayStyle.Flex : DisplayStyle.None;
            }
        }

        private void CreateGroupFromSelection()
        {
            if (selectedMaterials.Count == 0) return;
            var newGroup = new PropertyGroup { groupName = "Group " + (propertyGroups.Count + 1), isExpanded = true };
            foreach (var mat in selectedMaterials) newGroup.materials.Add(new MaterialEntry { material = mat, propertyName = "_BaseColor" });
            propertyGroups.Add(newGroup);
            RefreshGroupsUI();
            SwitchTab(1);
            selectedMaterials.Clear();
            UpdateCreateGroupButtonVisibility();
            PerformScan();
        }

        #endregion

        #region Editor Logic

        private void CreateEditorUI(VisualElement root)
        {
            editorContainer = new VisualElement { style = { flexGrow = 1, display = DisplayStyle.None } };
            var editorToolbar = new VisualElement();
            editorToolbar.AddToClassList("rex-row");
            editorToolbar.style.marginBottom = 10;

            var btnAddGroup = new Button(() => { propertyGroups.Add(new PropertyGroup()); RefreshGroupsUI(); }) { text = "Add Property Group" };
            btnAddGroup.AddToClassList("rex-flex-grow");

            var btnSave = new Button(SaveData) { text = "Save", tooltip = "Save groups to .asset" };
            var btnLoad = new Button(LoadData) { text = "Load", tooltip = "Load groups from .asset" };
            
            editorToolbar.Add(btnAddGroup);
            editorToolbar.Add(btnSave);
            editorToolbar.Add(btnLoad);

            editorContainer.Add(editorToolbar);
            groupsList = new ScrollView { style = { flexGrow = 1 } };
            editorContainer.Add(groupsList);

            root.Add(editorContainer);
        }

        private void SaveData()
        {
            string path = EditorUtility.SaveFilePanelInProject("Save Material Groups", "BatchMatGroups", "asset", "Save property groups to an asset file.");
            if (string.IsNullOrEmpty(path)) return;
            BatchMaterialData data = ScriptableObject.CreateInstance<BatchMaterialData>();
            data.propertyGroups = new List<PropertyGroup>();
            foreach (var g in propertyGroups) data.propertyGroups.Add(g.Clone());
            AssetDatabase.CreateAsset(data, path);
            AssetDatabase.SaveAssets();
            EditorGUIUtility.PingObject(data);
        }

        private void LoadData()
        {
            string path = EditorUtility.OpenFilePanel("Load Material Groups", Application.dataPath, "asset");
            if (string.IsNullOrEmpty(path)) return;
            if (path.StartsWith(Application.dataPath)) path = "Assets" + path.Substring(Application.dataPath.Length);
            BatchMaterialData data = AssetDatabase.LoadAssetAtPath<BatchMaterialData>(path);
            if (data != null)
            {
                propertyGroups.Clear();
                foreach (var g in data.propertyGroups) propertyGroups.Add(g.Clone());
                RefreshGroupsUI();
            }
        }

        private void RefreshGroupsUI()
        {
            groupsList.Clear();
            foreach (var group in propertyGroups) groupsList.Add(CreateGroupElement(group));
        }

        private VisualElement CreateGroupElement(PropertyGroup group)
        {
            var box = new VisualElement();
            box.AddToClassList("rex-box");

            var header = new VisualElement();
            header.AddToClassList("rex-row");
            header.style.marginBottom = 10;
            
            var nameField = new TextField { value = group.groupName };
            nameField.AddToClassList("rex-flex-grow");
            nameField.style.unityFontStyleAndWeight = FontStyle.Bold;
            nameField.RegisterValueChangedCallback(evt => group.groupName = evt.newValue);

            var dupBtn = new Button(() => {
                var newGroup = group.Clone();
                int index = propertyGroups.IndexOf(group);
                if (index >= 0) propertyGroups.Insert(index + 1, newGroup);
                else propertyGroups.Add(newGroup);
                RefreshGroupsUI();
            }) { text = "Copy", tooltip = "Duplicate Group" };
            dupBtn.AddToClassList("rex-button-small");

            var deleteBtn = new Button(() => { propertyGroups.Remove(group); RefreshGroupsUI(); }) { text = "X", tooltip = "Delete Group" };
            deleteBtn.AddToClassList("rex-button-small");
            deleteBtn.style.backgroundColor = new Color(0.7f, 0.2f, 0.2f, 0.3f);

            header.Add(nameField);
            header.Add(dupBtn);
            header.Add(deleteBtn);
            box.Add(header);

            var propTypeField = new EnumField("Property Type", group.propertyType);
            box.Add(propTypeField);

            var valRow = new VisualElement { style = { marginTop = 5, marginBottom = 10 } };
            var colorField = new ColorField("Value") { value = group.colorVal, style = { display = DisplayStyle.None } };
            var floatField = new FloatField("Value") { value = group.floatVal, style = { display = DisplayStyle.None } };
            var vectorField = new Vector4Field("Value") { value = group.vectorVal, style = { display = DisplayStyle.None } };
            var texField = new ObjectField("Value") { objectType = typeof(Texture), value = group.textureVal, style = { display = DisplayStyle.None } };
            valRow.Add(colorField);
            valRow.Add(floatField);
            valRow.Add(vectorField);
            valRow.Add(texField);
            box.Add(valRow);

            var matSection = new VisualElement();
            matSection.AddToClassList("rex-box");
            matSection.style.backgroundColor = new Color(0, 0, 0, 0.2f);
            matSection.style.marginBottom = 0;

            var matHeader = new VisualElement();
            matHeader.AddToClassList("rex-row");
            matHeader.style.justifyContent = Justify.SpaceBetween;

            var matList = new VisualElement { style = { display = group.isExpanded ? DisplayStyle.Flex : DisplayStyle.None } };
            Button collapseBtn = null;
            collapseBtn = new Button(() => {
                group.isExpanded = !group.isExpanded;
                matList.style.display = group.isExpanded ? DisplayStyle.Flex : DisplayStyle.None;
                collapseBtn.text = group.isExpanded ? "▼ MATERIALS" : "▶ MATERIALS";
            }) {
                text = group.isExpanded ? "▼ MATERIALS" : "▶ MATERIALS",
                style = { unityFontStyleAndWeight = FontStyle.Bold, backgroundColor = Color.clear, paddingLeft = 0, fontSize = 10, color = new Color(0.6f, 0.6f, 0.6f) }
            };
            collapseBtn.style.borderTopWidth = 0; collapseBtn.style.borderBottomWidth = 0; collapseBtn.style.borderLeftWidth = 0; collapseBtn.style.borderRightWidth = 0;

            var addMatBtn = new Button() { text = "+ ADD MATERIAL" };
            addMatBtn.AddToClassList("rex-button-small");
            matHeader.Add(collapseBtn);
            matHeader.Add(addMatBtn);
            matSection.Add(matHeader);
            matSection.Add(matList);
            box.Add(matSection);

            System.Action updateVisibility = () => {
                colorField.style.display = group.propertyType == MatPropType.Color ? DisplayStyle.Flex : DisplayStyle.None;
                floatField.style.display = group.propertyType == MatPropType.Float ? DisplayStyle.Flex : DisplayStyle.None;
                vectorField.style.display = group.propertyType == MatPropType.Vector ? DisplayStyle.Flex : DisplayStyle.None;
                texField.style.display = group.propertyType == MatPropType.Texture ? DisplayStyle.Flex : DisplayStyle.None;
            };

            propTypeField.RegisterValueChangedCallback(evt => { group.propertyType = (MatPropType)evt.newValue; updateVisibility(); });
            colorField.RegisterValueChangedCallback(evt => { group.colorVal = evt.newValue; ApplyValueToMaterials(group); });
            floatField.RegisterValueChangedCallback(evt => { group.floatVal = evt.newValue; ApplyValueToMaterials(group); });
            vectorField.RegisterValueChangedCallback(evt => { group.vectorVal = evt.newValue; ApplyValueToMaterials(group); });
            texField.RegisterValueChangedCallback(evt => { group.textureVal = (Texture)evt.newValue; ApplyValueToMaterials(group); });

            System.Action refreshMatList = null;
            refreshMatList = () => {
                matList.Clear();
                for (int i = 0; i < group.materials.Count; i++) {
                    int index = i;
                    var row = new VisualElement { style = { flexDirection = FlexDirection.Row, marginTop = 2 } };
                    var matField = new ObjectField { objectType = typeof(Material), value = group.materials[index].material, style = { flexGrow = 1, flexShrink = 1, minWidth = 0 } };
                    var propNameField = new TextField { value = group.materials[index].propertyName, tooltip = "Property Name", style = { width = 120, marginLeft = 5, flexShrink = 0 } };
                    var errIcon = new VisualElement { style = { width = 16, height = 16, marginLeft = 4, marginRight = 4, display = DisplayStyle.None, backgroundImage = (Texture2D)EditorGUIUtility.IconContent("console.erroricon.sml").image } };
                    var rmvBtn = new Button(() => { group.materials.RemoveAt(index); refreshMatList(); }) { text = "-" };
                    rmvBtn.AddToClassList("rex-button-small");
                    System.Action checkError = () => {
                        var mat = group.materials[index].material;
                        var prop = group.materials[index].propertyName;
                        bool hasProp = mat != null && !string.IsNullOrEmpty(prop) && mat.HasProperty(prop);
                        errIcon.style.display = (mat != null && !hasProp) ? DisplayStyle.Flex : DisplayStyle.None;
                    };
                    matField.RegisterValueChangedCallback(evt => { group.materials[index].material = (Material)evt.newValue; checkError(); });
                    propNameField.RegisterValueChangedCallback(evt => { group.materials[index].propertyName = evt.newValue; checkError(); });
                    row.Add(matField); row.Add(propNameField); row.Add(errIcon); row.Add(rmvBtn);
                    matList.Add(row); checkError();
                }
            };

            addMatBtn.clicked += () => {
                string lastPropName = "_BaseColor";
                if (group.materials.Count > 0) lastPropName = group.materials[group.materials.Count - 1].propertyName;
                group.materials.Add(new MaterialEntry() { propertyName = lastPropName });
                refreshMatList();
            };
            updateVisibility(); refreshMatList();
            return box;
        }

        private void ApplyValueToMaterials(PropertyGroup group)
        {
            foreach (var entry in group.materials)
            {
                var mat = entry.material;
                var prop = entry.propertyName;
                if (mat == null || string.IsNullOrEmpty(prop) || !mat.HasProperty(prop)) continue;
                Undo.RecordObject(mat, "Batch Update Property");
                switch (group.propertyType) {
                    case MatPropType.Color: mat.SetColor(prop, group.colorVal); break;
                    case MatPropType.Float: mat.SetFloat(prop, group.floatVal); break;
                    case MatPropType.Vector: mat.SetVector(prop, group.vectorVal); break;
                    case MatPropType.Texture: mat.SetTexture(prop, group.textureVal); break;
                }
                EditorUtility.SetDirty(mat);
            }
        }

        #endregion

        #region Replace Logic

        private void CreateReplaceUI(VisualElement root)
        {
            replaceContainer = new VisualElement { style = { flexGrow = 1, display = DisplayStyle.None } };
            var findReplaceBox = new VisualElement();
            findReplaceBox.AddToClassList("rex-box");
            var findLabel = new Label("SEARCH & REPLACE");
            findLabel.AddToClassList("rex-section-label");
            findReplaceBox.Add(findLabel);

            var findMatField = new ObjectField("Find Material") { objectType = typeof(Material), value = findMat };
            findMatField.RegisterValueChangedCallback(evt => findMat = (Material)evt.newValue);
            findReplaceBox.Add(findMatField);

            var replaceMatField = new ObjectField("Replace Material") { objectType = typeof(Material), value = replaceMat };
            replaceMatField.RegisterValueChangedCallback(evt => replaceMat = (Material)evt.newValue);
            findReplaceBox.Add(replaceMatField);

            var modeField = new EnumField("Replacement Mode", ReplaceMode.Scene);
            modeField.RegisterValueChangedCallback(evt => { replaceMode = (ReplaceMode)evt.newValue; affectedObjects.Clear(); RefreshAffectedListUI(); });
            findReplaceBox.Add(modeField);
            replaceContainer.Add(findReplaceBox);

            var btnScanReplace = new Button(InitReplaceScan) { text = "1. SCAN AFFECTED OBJECTS" };
            btnScanReplace.AddToClassList("rex-action-button"); btnScanReplace.AddToClassList("rex-action-button--pack");
            replaceContainer.Add(btnScanReplace);

            var affectedBox = new VisualElement();
            affectedBox.AddToClassList("rex-box"); affectedBox.style.flexGrow = 1;
            var affectedLabel = new Label("AFFECTED OBJECTS");
            affectedLabel.AddToClassList("rex-section-label"); affectedBox.Add(affectedLabel);
            affectedListView = new ScrollView(); affectedListView.AddToClassList("rex-result-list");
            affectedBox.Add(affectedListView);
            replaceContainer.Add(affectedBox);

            btnConvert = new Button(ExecuteReplace) { text = "2. START CONVERSION" };
            btnConvert.AddToClassList("rex-action-button"); btnConvert.AddToClassList("rex-action-button--unpack");
            btnConvert.SetEnabled(false);
            replaceContainer.Add(btnConvert);

            root.Add(replaceContainer);
        }

        private void InitReplaceScan()
        {
            affectedObjects.Clear();
            if (findMat == null) { EditorUtility.DisplayDialog("Error", "Assign a Find Material.", "OK"); return; }
            var renderers = FindObjectsByType<Renderer>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var r in renderers) if (Array.IndexOf(r.sharedMaterials, findMat) != -1) affectedObjects.Add(r.gameObject);
            btnConvert.SetEnabled(affectedObjects.Count > 0);
            RefreshAffectedListUI();
        }

        private void RefreshAffectedListUI()
        {
            affectedListView.Clear();
            if (affectedObjects.Count == 0) { affectedListView.Add(new Label("No assets affected.") { style = { marginTop = 20, alignSelf = Align.Center } }); return; }
            for (int i = 0; i < affectedObjects.Count; i++) {
                int index = i;
                var row = new VisualElement(); row.AddToClassList("rex-result-item");
                var field = new ObjectField { value = affectedObjects[index], objectType = typeof(GameObject), style = { flexGrow = 1 } }; field.SetEnabled(false);
                var rmvBtn = new Button(() => { affectedObjects.RemoveAt(index); RefreshAffectedListUI(); }) { text = "-" }; rmvBtn.AddToClassList("rex-button-small");
                row.Add(field); row.Add(rmvBtn); affectedListView.Add(row);
            }
        }

        private void ExecuteReplace()
        {
            if (findMat == null || replaceMat == null || affectedObjects.Count == 0) return;
            if (!EditorUtility.DisplayDialog("Confirm", $"Replace in {affectedObjects.Count} items?", "Yes", "Cancel")) return;
            foreach (var obj in affectedObjects) {
                if (obj is GameObject go && go.TryGetComponent<Renderer>(out var r)) {
                    var mats = r.sharedMaterials; bool changed = false;
                    for (int i = 0; i < mats.Length; i++) if (mats[i] == findMat) { mats[i] = replaceMat; changed = true; }
                    if (changed) { Undo.RecordObject(r, "Replace Material"); r.sharedMaterials = mats; EditorUtility.SetDirty(r); }
                }
            }
            AssetDatabase.SaveAssets(); InitReplaceScan();
        }

        #endregion

        #region Switcher Logic

        private void CreateSwitcherUI(VisualElement root)
        {
            switcherContainer = new VisualElement { style = { flexGrow = 1, display = DisplayStyle.None } };

            var configBox = new VisualElement();
            configBox.AddToClassList("rex-box");
            
            // Header row for label + preset icon
            var headerRow = new VisualElement();
            headerRow.style.flexDirection = FlexDirection.Row;
            headerRow.style.justifyContent = Justify.SpaceBetween;
            headerRow.style.alignItems = Align.Center;
            headerRow.style.marginBottom = 5;

            var configLabel = new Label("SHADER CONVERTER SETUP");
            configLabel.AddToClassList("rex-section-label");
            configLabel.style.marginBottom = 0;
            
            var presetBtn = RexTools.Editor.Core.RexPresetManager.CreatePresetIconButton(activeSwitcherSettings);
            
            headerRow.Add(configLabel);
            headerRow.Add(presetBtn);
            configBox.Add(headerRow);

            // Using local variables that sync to ScriptableObject
            var sourceShaderField = new ObjectField("Source Shader") { objectType = typeof(Shader), value = activeSwitcherSettings.sourceShader };
            sourceShaderField.RegisterValueChangedCallback(evt => {
                sourceShader = (Shader)evt.newValue;
                activeSwitcherSettings.sourceShader = sourceShader;
            });
            configBox.Add(sourceShaderField);

            var targetShaderField = new ObjectField("Target Shader") { objectType = typeof(Shader), value = activeSwitcherSettings.targetShader };
            targetShaderField.RegisterValueChangedCallback(evt => {
                targetShader = (Shader)evt.newValue;
                activeSwitcherSettings.targetShader = targetShader;
            });
            configBox.Add(targetShaderField);

            var previewRow = new VisualElement();
            previewRow.AddToClassList("rex-row");
            previewRow.style.marginTop = 5;

            var sourcePreviewField = new ObjectField("Source Mat (Preview)") { objectType = typeof(Material), value = activeSwitcherSettings.sourcePreviewMat };
            sourcePreviewField.AddToClassList("rex-flex-grow");
            sourcePreviewField.RegisterValueChangedCallback(evt => {
                sourcePreviewMat = (Material)evt.newValue;
                activeSwitcherSettings.sourcePreviewMat = sourcePreviewMat;
                if (sourcePreviewMat != null) {
                    sourceShader = sourcePreviewMat.shader;
                    activeSwitcherSettings.sourceShader = sourceShader;
                    sourceShaderField.value = sourceShader;
                }
            });
            previewRow.Add(sourcePreviewField);

            var targetPreviewField = new ObjectField("Target Mat (Preview)") { objectType = typeof(Material), value = activeSwitcherSettings.targetPreviewMat };
            targetPreviewField.AddToClassList("rex-flex-grow");
            targetPreviewField.RegisterValueChangedCallback(evt => {
                targetPreviewMat = (Material)evt.newValue;
                activeSwitcherSettings.targetPreviewMat = targetPreviewMat;
                if (targetPreviewMat != null) {
                    targetShader = targetPreviewMat.shader;
                    activeSwitcherSettings.targetShader = targetShader;
                    targetShaderField.value = targetShader;
                }
            });
            previewRow.Add(targetPreviewField);
            
            configBox.Add(previewRow);
            switcherContainer.Add(configBox);


            var toolbar = new VisualElement();
            toolbar.AddToClassList("rex-row");

            var btnLoadProps = new Button(LoadMappings) { text = "LOAD PROPERTIES" };
            btnLoadProps.AddToClassList("rex-flex-grow");
            toolbar.Add(btnLoadProps);

            var btnSmartMatch = new Button(SmartMatch) { text = "SMART MATCH" };
            btnSmartMatch.AddToClassList("rex-flex-grow");
            toolbar.Add(btnSmartMatch);

            switcherContainer.Add(toolbar);

            var mappingsBox = new VisualElement();
            mappingsBox.AddToClassList("rex-box");
            mappingsBox.style.flexGrow = 1;
            
            var mapLabel = new Label("PROPERTY MAPPINGS");
            mapLabel.AddToClassList("rex-section-label");
            mappingsBox.Add(mapLabel);

            switcherMappingList = new ScrollView();
            switcherMappingList.AddToClassList("rex-result-list");
            mappingsBox.Add(switcherMappingList);

            switcherContainer.Add(mappingsBox);

            var btnPerformSwitcher = new Button(PerformSwitcherConversion) { text = "CONVERT SELECTED MATERIALS" };
            btnPerformSwitcher.AddToClassList("rex-action-button");
            btnPerformSwitcher.AddToClassList("rex-action-button--pack");
            switcherContainer.Add(btnPerformSwitcher);

            root.Add(switcherContainer);
        }

        private void LoadMappings()
        {
            switcherMappingList.Clear();
            convMappings.Clear();

            Shader sShader = sourceShader != null ? sourceShader : (sourcePreviewMat != null ? sourcePreviewMat.shader : null);
            Shader tShader = targetShader != null ? targetShader : (targetPreviewMat != null ? targetPreviewMat.shader : null);

            if (sShader == null || tShader == null) {
                EditorUtility.DisplayDialog("Error", "Source and Target shaders must be assigned.", "OK");
                return;
            }

            int propCount = ShaderUtil.GetPropertyCount(sShader);
            int targetPropCount = ShaderUtil.GetPropertyCount(tShader);

            for (int i = 0; i < propCount; i++) {
                string name = ShaderUtil.GetPropertyName(sShader, i);
                var type = GetConvType(ShaderUtil.GetPropertyType(sShader, i));

                var options = new List<string> { "None" };
                for (int j = 0; j < targetPropCount; j++) {
                    if (GetConvType(ShaderUtil.GetPropertyType(tShader, j)) == type) {
                        options.Add(ShaderUtil.GetPropertyName(tShader, j));
                    }
                }

                convMappings.Add(new ConvPropertyMapping {
                    sourcePropName = name,
                    type = type,
                    targetOptions = options.ToArray(),
                    targetPropName = "None",
                    selectedIndex = 0
                });
            }
            RefreshMappingsUI();
        }

        private void RefreshMappingsUI()
        {
            switcherMappingList.Clear();
            foreach (var mapping in convMappings) {
                var row = new VisualElement();
                row.AddToClassList("rex-result-item");
                row.style.height = 24;

                var typeLabel = new Label(mapping.type.ToString().ToUpper().Substring(0, 3));
                typeLabel.style.width = 30; typeLabel.style.fontSize = 9; typeLabel.style.color = new Color(0.5f, 0.5f, 0.5f);
                row.Add(typeLabel);

                var sName = new Label(mapping.sourcePropName);
                sName.style.flexGrow = 1; sName.style.unityFontStyleAndWeight = FontStyle.Bold;
                row.Add(sName);

                var arrow = new Label("→"); arrow.style.marginRight = 5; arrow.style.marginLeft = 5;
                row.Add(arrow);

                var dropdown = new PopupField<string>(mapping.targetOptions.ToList(), mapping.selectedIndex);
                dropdown.style.width = 180;
                dropdown.RegisterValueChangedCallback(evt => {
                    mapping.targetPropName = evt.newValue;
                    mapping.selectedIndex = dropdown.index;
                    mapping.isValid = mapping.targetPropName != "None";
                    SyncMappingsToSettings();
                });
                row.Add(dropdown);

                switcherMappingList.Add(row);
            }
        }

        private void SyncMappingsToSettings()
        {
            if (activeSwitcherSettings == null) return;
            activeSwitcherSettings.propertyPairs.Clear();
            foreach (var m in convMappings)
            {
                if (m.isValid)
                {
                    activeSwitcherSettings.propertyPairs.Add(new PropertyPair 
                    { 
                        sourceProperty = m.sourcePropName, 
                        targetProperty = m.targetPropName, 
                        propertyType = (int)m.type,
                        selectedIndex = m.selectedIndex
                    });
                }
            }
        }

        private void SmartMatch()
        {
            foreach (var m in convMappings) {
                string best = m.targetOptions.FirstOrDefault(opt => opt.Equals(m.sourcePropName, StringComparison.OrdinalIgnoreCase));
                if (best == null) {
                    string cleanSource = m.sourcePropName.TrimStart('_');
                    best = m.targetOptions.FirstOrDefault(opt => opt.TrimStart('_').Equals(cleanSource, StringComparison.OrdinalIgnoreCase));
                }
                if (best != null) {
                    m.targetPropName = best;
                    m.selectedIndex = Array.IndexOf(m.targetOptions, best);
                    m.isValid = true;
                }
            }
            SyncMappingsToSettings();
            RefreshMappingsUI();
        }

        private ConvPropertyType GetConvType(ShaderUtil.ShaderPropertyType type)
        {
            return type switch {
                ShaderUtil.ShaderPropertyType.Color => ConvPropertyType.Color,
                ShaderUtil.ShaderPropertyType.Vector => ConvPropertyType.Vector,
                ShaderUtil.ShaderPropertyType.TexEnv => ConvPropertyType.Texture,
                _ => ConvPropertyType.Float
            };
        }

        // Old methods kept for compatibility or removed if no longer used
        private void SaveSwitcherPreset() { /* RexPresetManager handles this now */ }
        private void LoadSwitcherPreset() { /* RexPresetManager handles this now */ }

        private void PerformSwitcherConversion()
        {
            var mats = Selection.objects.OfType<Material>().ToList();
            if (mats.Count == 0) { EditorUtility.DisplayDialog("Error", "Select materials in Project window.", "OK"); return; }
            if (targetShader == null) { EditorUtility.DisplayDialog("Error", "Target shader not set.", "OK"); return; }

            Undo.RecordObjects(mats.ToArray(), "Batch Switch Shader");
            foreach (var mat in mats) {
                Dictionary<string, (Texture, Vector2, Vector2)> texStore = new Dictionary<string, (Texture, Vector2, Vector2)>();
                foreach (var m in convMappings) {
                    if (m.type == ConvPropertyType.Texture && mat.HasProperty(m.sourcePropName))
                        texStore[m.sourcePropName] = (mat.GetTexture(m.sourcePropName), mat.GetTextureOffset(m.sourcePropName), mat.GetTextureScale(m.sourcePropName));
                }

                mat.shader = targetShader;

                foreach (var m in convMappings) {
                    if (!m.isValid || !mat.HasProperty(m.targetPropName)) continue;
                    switch (m.type) {
                        case ConvPropertyType.Color: mat.SetColor(m.targetPropName, mat.GetColor(m.sourcePropName)); break;
                        case ConvPropertyType.Float: mat.SetFloat(m.targetPropName, mat.GetFloat(m.sourcePropName)); break;
                        case ConvPropertyType.Vector: mat.SetVector(m.targetPropName, mat.GetVector(m.sourcePropName)); break;
                        case ConvPropertyType.Texture: 
                            if (texStore.TryGetValue(m.sourcePropName, out var data)) {
                                mat.SetTexture(m.targetPropName, data.Item1);
                                mat.SetTextureOffset(m.targetPropName, data.Item2);
                                mat.SetTextureScale(m.targetPropName, data.Item3);
                            }
                            break;
                    }
                }
                EditorUtility.SetDirty(mat);
            }
            AssetDatabase.SaveAssets();
            EditorUtility.DisplayDialog("Success", $"Converted {mats.Count} materials.", "OK");
        }

        #endregion
    }

    public class BatchMaterialData : ScriptableObject
    {
        public List<BatchMaterialEditorWindow.PropertyGroup> propertyGroups = new List<BatchMaterialEditorWindow.PropertyGroup>();
    }
}
