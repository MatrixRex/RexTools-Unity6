using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using RexTools.BatchMaterialEditor.Editor.Tabs;
using RexTools.Editor.Core;

namespace RexTools.BatchMaterialEditor.Editor
{
    public class BatchMaterialEditorWindow : EditorWindow
    {
        // Global Data explicitly exposed via Preset target
        private MaterialEditorPreset activeEditorPreset;
        public List<PropertyGroup> PropertyGroups => activeEditorPreset != null ? activeEditorPreset.propertyGroups : null;
        public MaterialEditorPreset EditorPreset => activeEditorPreset;
        public HashSet<Material> SelectedMaterials { get; private set; } = new HashSet<Material>();

        // Tab Managers
        private ScannerTab scannerTab;
        private EditorTab editorTab;
        private ReplacerTab replacerTab;
        private ConverterTab converterTab;

        private RexTabGroup tabGroup;
        
        private RexHelpBox helpBox;
        private bool showHelp = false;

        [MenuItem("Tools/Rex Tools/Batch Material Editor")]
        public static void ShowWindow()
        {
            BatchMaterialEditorWindow wnd = GetWindow<BatchMaterialEditorWindow>();
            wnd.titleContent = new GUIContent("Batch Material Editor");
            wnd.minSize = new Vector2(450, 600);
        }

        public void CreateGUI()
        {
            if (activeEditorPreset == null)
            {
                activeEditorPreset = ScriptableObject.CreateInstance<MaterialEditorPreset>();
                activeEditorPreset.name = "MaterialEditorPreset_Runtime";
            }

            VisualElement root = rootVisualElement;

            // Load UXML
            string uxmlPath = AssetDatabase.GUIDToAssetPath("75494d496a793c5448375494d496a793"); // I'll use a path instead if I don't know the GUID
            // Better to use path for now
            string windowPath = "Editor/Batch Material Editor/BatchMaterialEditorWindow.uxml";
            string fullPath = "Assets/" + windowPath;
            if (!AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(fullPath))
            {
                // Try package path
                fullPath = "Packages/com.matrixrex.rextools/" + windowPath;
            }

            var visualTree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(fullPath);
            if (visualTree != null)
            {
                visualTree.CloneTree(root);
            }
            else
            {
                root.Add(new Label("Could not load BatchMaterialEditorWindow.uxml"));
                return;
            }

            // Load Global Styles
            string[] possibleStyles = {
                "Packages/com.matrixrex.rextools/Editor/RexToolsStyles.uss",
                "Assets/Editor/RexToolsStyles.uss"
            };
            StyleSheet globalStyleSheet = null;
            foreach (var path in possibleStyles) {
                globalStyleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(path);
                if (globalStyleSheet != null) break;
            }
            if (globalStyleSheet != null) root.styleSheets.Add(globalStyleSheet);

            // Bind Elements
            var contentContainer = root.Q<VisualElement>("content-container");
            
            // --- BRANDED HEADER & HELP BOX ---
            var helpBoxContainer = root.Q<VisualElement>("help-box-container");
            if (helpBoxContainer != null)
            {
                helpBox = new RexHelpBox();
                helpBoxContainer.Add(helpBox);
            }

            var headerContainer = root.Q<VisualElement>("header-container");
            if (headerContainer != null)
            {
                var header = new RexHeader("Batch Material Editor", showHelpButton: true);
                header.OnHelpClicked += () => {
                    showHelp = !showHelp;
                    helpBox?.ToggleVisibility();
                    header.SetHelpButtonActive(showHelp);
                };
                headerContainer.Add(header);
            }

            // Bind Tabs
            var tabsContainer = root.Q<VisualElement>("tabs-container");
            if (tabsContainer != null)
            {
                tabGroup = new RexTabGroup(new string[] { "Scanner", "Editor", "Replacer", "Converter" });
                tabGroup.OnTabChanged += SwitchToTab;
                tabsContainer.Add(tabGroup);
            }

            // Initialize Tabs
            scannerTab = new ScannerTab(this, contentContainer);
            editorTab = new EditorTab(this, contentContainer);
            replacerTab = new ReplacerTab(this, contentContainer);
            converterTab = new ConverterTab(this, contentContainer);

            SwitchToTab(0);
        }

        private void OnInspectorUpdate()
        {
            // Forward update pump to tabs
            editorTab?.OnInspectorUpdate();
            converterTab?.OnInspectorUpdate();
        }

        public void SwitchToTab(int index)
        {
            scannerTab.SetDisplay(index == 0);
            editorTab.SetDisplay(index == 1);
            replacerTab.SetDisplay(index == 2);
            converterTab.SetDisplay(index == 3);

            tabGroup?.SetSelectedTabWithoutNotify(index);
            
            UpdateHelpContext(index);
        }

        private void UpdateHelpContext(int index)
        {
            if (helpBox == null) return;
            helpBox.Clear();

            var helpTitle = new Label("HOW TO USE:");
            helpTitle.AddToClassList("rex-help-text-title");
            helpBox.Add(helpTitle);

            string[] helpLines = new string[0];
            
            switch (index)
            {
                case 0: // Scanner
                    helpLines = new string[] {
                        "Extracts all unique materials used in the opened scenes",
                        "Send selected materials directly to the Editor or Replace tabs."
                    };
                    break;
                case 1: // Editor
                    helpLines = new string[] {
                        "Batch edit shared material properties.",
                        "Create a new property group and assign materials.",
                        "Modify a property uniformly across all materials in the group.",
                        "Save and load group presets."
                    };
                    break;
                case 2: // Replace
                    helpLines = new string[] {
                        "Batch replace one material with another material.",
                        "Set 'Find Material' and 'Replace Material'.",
                        "Replacement Mode: Scene. Only replace scene material instances.",
                        "Replacement Mode: Prefab. Edit each prefabs material instances.",
                        "Replacement Mode: New Prefab. Create new prefabs with replaced materials. Keeping originals intact.",
                        "Press Scan Objects to see which objects are affected.",
                        "Start conversion to actually replace materials."
                    };
                    break;
                case 3: // Converter
                    helpLines = new string[] {
                        "Batch-convert materials to a new shader.",
                        "Map properties from the old shader to the new one.",
                        "Save your mapping configuration as a Preset.",
                        "Select a gameojbect to get all materials in the hierarchy to convert",
                        "Select a material to replace only that material."
                    };
                    break;
            }

            foreach (var line in helpLines)
            {
                helpBox.AddHelpLine(line);
            }
        }

        public void RefreshGroupsUI()
        {
            editorTab?.RefreshGroupsUI();
        }
    }
}
