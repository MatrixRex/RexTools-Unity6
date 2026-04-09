using UnityEditor.UIElements;
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

        public ConverterUI(VisualElement container, MaterialConverterPreset preset)
        {
            Root = new VisualElement { style = { flexGrow = 1, display = DisplayStyle.None } };

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
            
            var presetBtn = RexTools.Editor.Core.RexPresetManager.CreatePresetIconButton(preset);
            
            headerRow.Add(configLabel);
            headerRow.Add(presetBtn);
            configBox.Add(headerRow);

            // Merged Shader/Material Fields (Drop Areas)
            var dropCol = new VisualElement();
            dropCol.style.flexDirection = FlexDirection.Column;
            dropCol.style.marginTop = 5;

            SourceDrop = new ObjectField("Source (Mat/Shader)") { objectType = typeof(UnityEngine.Object), value = preset.sourcePreviewMat != null ? (UnityEngine.Object)preset.sourcePreviewMat : preset.sourceShader };
            dropCol.Add(SourceDrop);

            TargetDrop = new ObjectField("Target (Mat/Shader)") { objectType = typeof(UnityEngine.Object), value = preset.targetPreviewMat != null ? (UnityEngine.Object)preset.targetPreviewMat : preset.targetShader };
            dropCol.Add(TargetDrop);

            configBox.Add(dropCol);
            Root.Add(configBox);

            var toolbar = new VisualElement();
            toolbar.AddToClassList("rex-row");

            BtnLoadProps = new Button { text = "LOAD PROPERTIES" };
            BtnLoadProps.AddToClassList("rex-flex-grow");
            toolbar.Add(BtnLoadProps);

            Root.Add(toolbar);

            var mappingsBox = new VisualElement();
            mappingsBox.AddToClassList("rex-box");
            mappingsBox.style.flexGrow = 1;
            
            var mapLabel = new Label("PROPERTY MAPPINGS");
            mapLabel.AddToClassList("rex-section-label");
            mappingsBox.Add(mapLabel);

            SwitcherMappingList = new ScrollView();
            SwitcherMappingList.AddToClassList("rex-result-list");
            mappingsBox.Add(SwitcherMappingList);

            Root.Add(mappingsBox);

            BtnPerformConverter = new Button { text = "CONVERT SELECTED MATERIALS" };
            BtnPerformConverter.AddToClassList("rex-action-button");
            BtnPerformConverter.AddToClassList("rex-action-button--pack");
            Root.Add(BtnPerformConverter);

            container.Add(Root);
        }

        public void PopulateMappings(System.Collections.Generic.List<ConvPropertyMapping> mappings, UnityEngine.Material sourcePreviewMat, System.Action onMappingChanged)
        {
            SwitcherMappingList.Clear();
            foreach (var mapping in mappings) {
                var row = new VisualElement();
                row.AddToClassList("rex-result-item");
                row.style.height = 36; // Extra height for preview box

                // 1. Type
                var typeLabel = new Label(mapping.type.ToString().ToUpper().Substring(0, 3));
                typeLabel.style.width = 30; typeLabel.style.fontSize = 9; typeLabel.style.color = new UnityEngine.Color(0.5f, 0.5f, 0.5f);
                row.Add(typeLabel);

                // 2. Preview Values Box
                var previewBox = new VisualElement();
                previewBox.style.width = 24; previewBox.style.height = 24;
                previewBox.style.marginRight = 10;
                previewBox.style.borderTopWidth = 1;
                previewBox.style.borderBottomWidth = 1;
                previewBox.style.borderLeftWidth = 1;
                previewBox.style.borderRightWidth = 1;

                var borderColor = new UnityEngine.Color(0.2f, 0.2f, 0.2f);
                previewBox.style.borderTopColor = borderColor;
                previewBox.style.borderBottomColor = borderColor;
                previewBox.style.borderLeftColor = borderColor;
                previewBox.style.borderRightColor = borderColor;
                previewBox.style.backgroundColor = new UnityEngine.Color(0.15f, 0.15f, 0.15f);

                if (sourcePreviewMat != null && sourcePreviewMat.HasProperty(mapping.sourcePropName)) {
                    if (mapping.type == ConvPropertyType.Color) {
                        previewBox.style.backgroundColor = sourcePreviewMat.GetColor(mapping.sourcePropName);
                    } else if (mapping.type == ConvPropertyType.Texture) {
                        var tex = sourcePreviewMat.GetTexture(mapping.sourcePropName);
                        if (tex != null && tex is UnityEngine.Texture2D t2d) {
                            previewBox.style.backgroundImage = t2d;
                        }
                    } else if (mapping.type == ConvPropertyType.Float) {
                        var floatLabel = new Label(sourcePreviewMat.GetFloat(mapping.sourcePropName).ToString("0.0"));
                        floatLabel.style.fontSize = 9; floatLabel.style.unityTextAlign = UnityEngine.TextAnchor.MiddleCenter; 
                        floatLabel.style.color = UnityEngine.Color.white;
                        floatLabel.style.marginTop = 4; // center rough
                        previewBox.Add(floatLabel);
                    }
                }
                row.Add(previewBox);

                // 3. Name Column
                var nameCol = new VisualElement();
                nameCol.style.flexGrow = 1;

                var descName = new Label(mapping.sourcePropDesc);
                descName.style.unityFontStyleAndWeight = UnityEngine.FontStyle.Bold;
                descName.style.fontSize = 12;
                nameCol.Add(descName);

                var internalName = new Label(mapping.sourcePropName);
                internalName.style.color = new UnityEngine.Color(0.6f, 0.6f, 0.6f);
                internalName.style.fontSize = 9;
                internalName.style.marginTop = -2;
                nameCol.Add(internalName);

                row.Add(nameCol);

                var arrow = new Label("→"); arrow.style.marginRight = 5; arrow.style.marginLeft = 5;
                row.Add(arrow);

                // 4. Dropdown
                var dropdown = new PopupField<string>(new System.Collections.Generic.List<string>(mapping.targetDisplayOptions), mapping.selectedIndex);
                dropdown.style.width = 200;
                dropdown.RegisterValueChangedCallback(evt => {
                    mapping.selectedIndex = dropdown.index;
                    mapping.targetPropName = mapping.targetOptions[dropdown.index];
                    mapping.isValid = mapping.targetPropName != "None";
                    onMappingChanged?.Invoke();
                });
                row.Add(dropdown);

                SwitcherMappingList.Add(row);
            }
        }
    }
}
