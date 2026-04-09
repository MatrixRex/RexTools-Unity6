using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using RexTools.Editor.Core;

namespace RexTools.BatchMaterialEditor.Editor.Tabs
{
    public class ConverterUI
    {
        public VisualElement Root { get; private set; }
        public ObjectField SourceDrop { get; private set; }
        public ObjectField TargetDrop { get; private set; }
        public Button BtnLoadProps { get; private set; }
        public ScrollView SwitcherMappingList { get; private set; }
        public Button BtnPerformConverter { get; private set; }

        private VisualTreeAsset mappingTemplate;

        public ConverterUI(VisualElement container, MaterialConverterPreset preset)
        {
            // Load Tab UXML
            string tabPath = "Editor/Batch Material Editor/Tabs/Converter/ConverterTab.uxml";
            string fullTabPath = "Assets/" + tabPath;
            var visualTree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(fullTabPath);
            if (visualTree == null) visualTree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Packages/com.matrixrex.rextools/" + tabPath);

            if (visualTree != null)
            {
                Root = visualTree.CloneTree().Q<VisualElement>("converter-tab-root");
                
                var presetContainer = Root.Q<VisualElement>("preset-btn-container");
                if (presetContainer != null)
                {
                    var presetBtn = RexTools.Editor.Core.RexPresetManager.CreatePresetIconButton(preset);
                    presetContainer.Add(presetBtn);
                }

                SourceDrop = Root.Q<ObjectField>("source-drop");
                SourceDrop.objectType = typeof(UnityEngine.Object);
                SourceDrop.value = preset.sourcePreviewMat != null ? (UnityEngine.Object)preset.sourcePreviewMat : preset.sourceShader;

                TargetDrop = Root.Q<ObjectField>("target-drop");
                TargetDrop.objectType = typeof(UnityEngine.Object);
                TargetDrop.value = preset.targetPreviewMat != null ? (UnityEngine.Object)preset.targetPreviewMat : preset.targetShader;

                BtnLoadProps = Root.Q<Button>("btn-load-props");
                SwitcherMappingList = Root.Q<ScrollView>("mapping-list");
                BtnPerformConverter = Root.Q<Button>("btn-convert");
            }
            else
            {
                Root = new VisualElement();
                Root.Add(new Label("Could not load ConverterTab.uxml"));
                SourceDrop = new ObjectField();
                TargetDrop = new ObjectField();
                BtnLoadProps = new Button();
                SwitcherMappingList = new ScrollView();
                BtnPerformConverter = new Button();
            }

            // Load Mapping Template
            string mappingPath = "Editor/Batch Material Editor/Tabs/Converter/ConverterMappingRow.uxml";
            string fullMappingPath = "Assets/" + mappingPath;
            mappingTemplate = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(fullMappingPath);
            if (mappingTemplate == null) mappingTemplate = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Packages/com.matrixrex.rextools/" + mappingPath);

            container.Add(Root);
        }

        public void PopulateMappings(System.Collections.Generic.List<ConvPropertyMapping> mappings, UnityEngine.Material sourcePreviewMat, System.Action onMappingChanged)
        {
            SwitcherMappingList.Clear();
            if (mappingTemplate == null) return;

            foreach (var mapping in mappings) {
                var row = mappingTemplate.CloneTree().ElementAt(0);

                row.Q<Label>("type-label").text = mapping.type.ToString().ToUpper().Substring(0, 3);
                
                var previewBox = row.Q<VisualElement>("preview-box");
                var previewText = row.Q<Label>("preview-text");

                if (sourcePreviewMat != null && sourcePreviewMat.HasProperty(mapping.sourcePropName)) {
                    if (mapping.type == MatPropType.Color) {
                        previewBox.style.backgroundColor = sourcePreviewMat.GetColor(mapping.sourcePropName);
                    } else if (mapping.type == MatPropType.Texture) {
                        var tex = sourcePreviewMat.GetTexture(mapping.sourcePropName);
                        if (tex != null && tex is UnityEngine.Texture2D t2d) {
                            previewBox.style.backgroundImage = t2d;
                        }
                    } else if (mapping.type == MatPropType.Float) {
                        previewText.text = sourcePreviewMat.GetFloat(mapping.sourcePropName).ToString("0.0");
                    }
                }

                row.Q<Label>("prop-desc").text = mapping.sourcePropDesc;
                row.Q<Label>("prop-name").text = mapping.sourcePropName;

                var dropdownContainer = row.Q<VisualElement>("dropdown-container");
                var dropdown = new PopupField<string>(new System.Collections.Generic.List<string>(mapping.targetDisplayOptions), mapping.selectedIndex);
                dropdown.style.flexGrow = 1;
                dropdown.RegisterValueChangedCallback(evt => {
                    mapping.selectedIndex = dropdown.index;
                    mapping.targetPropName = mapping.targetOptions[dropdown.index];
                    mapping.isValid = mapping.targetPropName != "None";
                    onMappingChanged?.Invoke();
                });
                dropdownContainer.Add(dropdown);

                SwitcherMappingList.Add(row);
            }
        }
    }
}
