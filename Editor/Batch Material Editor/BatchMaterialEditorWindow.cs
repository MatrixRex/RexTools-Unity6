using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using RexTools.BatchMaterialEditor.Editor.Tabs;

namespace RexTools.BatchMaterialEditor.Editor
{
    public class BatchMaterialEditorWindow : EditorWindow
    {
        // Global Data explicitly exposed
        public List<PropertyGroup> PropertyGroups { get; private set; } = new List<PropertyGroup>();
        public HashSet<Material> SelectedMaterials { get; private set; } = new HashSet<Material>();

        // Tab Managers
        private ScannerTab scannerTab;
        private EditorTab editorTab;
        private ReplaceTab replaceTab;
        private ConverterTab converterTab;

        private List<Button> tabButtons = new List<Button>();

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

            string[] tabNames = { "Scanner", "Editor", "Replace", "Converter" };
            for (int i = 0; i < tabNames.Length; i++)
            {
                int index = i;
                var btn = new Button(() => SwitchToTab(index)) { text = tabNames[i] };
                btn.AddToClassList("rex-tab-button");
                tabButtons.Add(btn);
                tabsContainer.Add(btn);
            }
            root.Add(tabsContainer);

            // --- TAB CONTENT CONTAINER ---
            var contentContainer = new VisualElement();
            contentContainer.AddToClassList("rex-tab-content-container");
            root.Add(contentContainer);

            // Initialize Tabs
            scannerTab = new ScannerTab(this, contentContainer);
            editorTab = new EditorTab(this, contentContainer);
            replaceTab = new ReplaceTab(this, contentContainer);
            converterTab = new ConverterTab(this, contentContainer);

            SwitchToTab(0);
        }

        private void OnInspectorUpdate()
        {
            // Forward update pump to converter
            converterTab?.OnInspectorUpdate();
        }

        public void SwitchToTab(int index)
        {
            scannerTab.SetDisplay(index == 0);
            editorTab.SetDisplay(index == 1);
            replaceTab.SetDisplay(index == 2);
            converterTab.SetDisplay(index == 3);

            for (int i = 0; i < tabButtons.Count; i++)
            {
                tabButtons[i].RemoveFromClassList("rex-tab-button--active");
                tabButtons[i].RemoveFromClassList("rex-tab-button--inactive");
                tabButtons[i].AddToClassList(i == index ? "rex-tab-button--active" : "rex-tab-button--inactive");
            }
        }

        public void RefreshGroupsUI()
        {
            editorTab?.RefreshGroupsUI();
        }
    }
}
