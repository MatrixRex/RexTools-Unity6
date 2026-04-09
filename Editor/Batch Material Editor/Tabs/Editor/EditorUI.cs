using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace RexTools.BatchMaterialEditor.Editor.Tabs
{
    public class EditorUI
    {
        public VisualElement Root { get; private set; }
        public Button BtnAddGroup { get; private set; }
        public VisualElement GroupsList { get; private set; }

        private VisualTreeAsset tabTemplate;
        private VisualTreeAsset groupTemplate;

        public EditorUI(VisualElement container, MaterialEditorPreset preset)
        {
            // Load Tab UXML
            string tabPath = "Editor/Batch Material Editor/Tabs/Editor/EditorTab.uxml";
            string fullTabPath = "Assets/" + tabPath;
            tabTemplate = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(fullTabPath);
            if (tabTemplate == null) tabTemplate = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Packages/com.matrixrex.rextools/" + tabPath);

            if (tabTemplate != null)
            {
                Root = tabTemplate.CloneTree().Q<VisualElement>("editor-tab-root");
                BtnAddGroup = Root.Q<Button>("btn-add-group");
                GroupsList = Root.Q<VisualElement>("groups-list");

                var presetContainer = Root.Q<VisualElement>("preset-btn-container");
                if (presetContainer != null)
                {
                    var presetBtn = RexTools.Editor.Core.RexPresetManager.CreatePresetIconButton(preset, "Editor Groups Preset");
                    presetContainer.Add(presetBtn);
                }
            }
            else
            {
                Root = new VisualElement();
                Root.Add(new Label("Could not load EditorTab.uxml"));
                BtnAddGroup = new Button();
                GroupsList = new VisualElement();
            }

            // Load Group Template UXML
            string groupPath = "Editor/Batch Material Editor/Tabs/Editor/MaterialPropertyGroup.uxml";
            string fullGroupPath = "Assets/" + groupPath;
            groupTemplate = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(fullGroupPath);
            if (groupTemplate == null) groupTemplate = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Packages/com.matrixrex.rextools/" + groupPath);

            container.Add(Root);
        }

        // Factory function for dynamic elements
        public VisualElement CreateGroupElement(PropertyGroup group, System.Action onDuplicate, System.Action onDelete, System.Action<PropertyGroup> onApplyValue, System.Action onAddMaterial, System.Action<System.Collections.Generic.List<Material>> onDropMaterials, System.Action refreshRoot)
        {
            if (groupTemplate == null) return new Label("Group template missing");

            var box = groupTemplate.CloneTree().ElementAt(0);
            
            var nameField = box.Q<TextField>("group-name");
            nameField.value = group.groupName;
            nameField.RegisterValueChangedCallback(evt => group.groupName = evt.newValue);

            box.Q<Button>("btn-duplicate").clicked += onDuplicate;
            box.Q<Button>("btn-delete").clicked += onDelete;

            var propTypeField = box.Q<EnumField>("prop-type");
            propTypeField.Init(group.propertyType);
            
            var colorField = box.Q<ColorField>("color-value");
            var floatField = box.Q<FloatField>("float-value");
            var vectorField = box.Q<Vector4Field>("vector-value");
            var texField = box.Q<ObjectField>("texture-value");
            texField.objectType = typeof(Texture);

            colorField.value = group.colorVal;
            floatField.value = group.floatVal;
            vectorField.value = group.vectorVal;
            texField.value = group.textureVal;

            var matSection = box.Q<VisualElement>("mat-section");
            var matList = box.Q<VisualElement>("mat-list");
            var collapseBtn = box.Q<Button>("btn-collapse");
            var addMatBtn = box.Q<Button>("btn-add-material");

            matList.style.display = group.isExpanded ? DisplayStyle.Flex : DisplayStyle.None;
            collapseBtn.text = group.isExpanded ? "▼ MATERIALS" : "▶ MATERIALS";
            collapseBtn.clicked += () => {
                group.isExpanded = !group.isExpanded;
                matList.style.display = group.isExpanded ? DisplayStyle.Flex : DisplayStyle.None;
                collapseBtn.text = group.isExpanded ? "▼ MATERIALS" : "▶ MATERIALS";
            };

            addMatBtn.clicked += onAddMaterial;

            System.Action refreshMatListUI = null;
            System.Action updateVisibility = () => {
                colorField.EnableInClassList("rex-hidden", group.propertyType != MatPropType.Color);
                floatField.EnableInClassList("rex-hidden", group.propertyType != MatPropType.Float);
                vectorField.EnableInClassList("rex-hidden", group.propertyType != MatPropType.Vector);
                texField.EnableInClassList("rex-hidden", group.propertyType != MatPropType.Texture);
            };

            matSection.RegisterCallback<DragUpdatedEvent>(evt => {
                if (DragAndDrop.objectReferences.Length > 0)
                {
                    DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
                    evt.StopPropagation();
                }
            });

            matSection.RegisterCallback<DragPerformEvent>(evt => {
                DragAndDrop.AcceptDrag();
                var droppedMats = new System.Collections.Generic.List<Material>();
                foreach (var obj in DragAndDrop.objectReferences)
                {
                    if (obj is Material m) droppedMats.Add(m);
                    else if (obj is GameObject go)
                    {
                        var renderers = go.GetComponentsInChildren<Renderer>(true);
                        foreach (var r in renderers)
                        {
                            foreach (var mat in r.sharedMaterials)
                            {
                                if (mat != null) droppedMats.Add(mat);
                            }
                        }
                    }
                }
                
                if (droppedMats.Count > 0)
                {
                    onDropMaterials?.Invoke(droppedMats);
                    refreshMatListUI?.Invoke();
                }
                evt.StopPropagation();
            });

            propTypeField.RegisterValueChangedCallback(evt => { 
                group.propertyType = (MatPropType)evt.newValue; 
                updateVisibility(); 
                refreshMatListUI?.Invoke(); 
            });

            colorField.RegisterValueChangedCallback(evt => { group.colorVal = evt.newValue; onApplyValue?.Invoke(group); });
            floatField.RegisterValueChangedCallback(evt => { group.floatVal = evt.newValue; onApplyValue?.Invoke(group); });
            vectorField.RegisterValueChangedCallback(evt => { group.vectorVal = evt.newValue; onApplyValue?.Invoke(group); });
            texField.RegisterValueChangedCallback(evt => { group.textureVal = (Texture)evt.newValue; onApplyValue?.Invoke(group); });

            refreshMatListUI = () => {
                matList.Clear();
                for (int i = 0; i < group.materials.Count; i++)
                {
                    int index = i;
                    var row = new VisualElement { style = { flexDirection = FlexDirection.Row, marginTop = 2 } };
                    var matField = new ObjectField { objectType = typeof(Material), value = group.materials[index].material, style = { flexGrow = 1, flexShrink = 1, minWidth = 0 } };
                    var propNameFieldContainer = new VisualElement { style = { width = 120, marginLeft = 5, flexShrink = 0 } };
                    
                    var errIcon = new VisualElement { style = { width = 16, height = 16, marginLeft = 4, marginRight = 4, display = DisplayStyle.None, backgroundImage = (Texture2D)EditorGUIUtility.IconContent("console.erroricon.sml").image } };
                    
                    var rmvBtn = new Button(() => { group.materials.RemoveAt(index); refreshMatListUI(); }) { tooltip = "Remove Material" };
                    rmvBtn.AddToClassList("rex-icon-button");
                    var rmvIcon = new VisualElement();
                    rmvIcon.AddToClassList("rex-icon-remove");
                    rmvBtn.Add(rmvIcon);
                    
                    System.Collections.Generic.List<string> currentNames = new System.Collections.Generic.List<string> { "None" };
                    PopupField<string> propNameField = new PopupField<string>(new System.Collections.Generic.List<string> { "None" }, 0);
                    propNameField.style.flexGrow = 1;
                    propNameFieldContainer.Add(propNameField);

                    System.Action refreshDropdown = () => {
                        var mat = group.materials[index].material;
                        if (mat != null && mat.shader != null) {
                            var props = BatchMaterialEditorHelpers.GetShaderProperties(mat.shader, group.propertyType);
                            currentNames = props.Names;
                            propNameField.choices = props.DisplayNames;
                            
                            int selIndex = currentNames.IndexOf(group.materials[index].propertyName);
                            if (selIndex == -1) selIndex = 0;
                            propNameField.index = selIndex;
                        } else {
                            currentNames = new System.Collections.Generic.List<string> { "None" };
                            propNameField.choices = currentNames;
                            propNameField.index = 0;
                        }
                    };
                    
                    System.Action checkError = () => {
                        var mat = group.materials[index].material;
                        var prop = group.materials[index].propertyName;
                        bool hasProp = mat != null && !string.IsNullOrEmpty(prop) && prop != "None" && mat.HasProperty(prop);
                        errIcon.style.display = (mat != null && !hasProp) ? DisplayStyle.Flex : DisplayStyle.None;
                    };
                    
                    propNameField.RegisterValueChangedCallback(evt => {
                        if (propNameField.index >= 0 && propNameField.index < currentNames.Count) {
                            group.materials[index].propertyName = currentNames[propNameField.index];
                        }
                        checkError();
                    });

                    matField.RegisterValueChangedCallback(evt => { 
                        group.materials[index].material = (Material)evt.newValue; 
                        refreshDropdown();
                        checkError(); 
                    });
                    
                    refreshDropdown();
                    checkError();

                    row.Add(matField); row.Add(propNameFieldContainer); row.Add(errIcon); row.Add(rmvBtn);
                    matList.Add(row);
                }
            };

            addMatBtn.clicked += refreshMatListUI;

            updateVisibility(); 
            refreshMatListUI();
            return box;
        }
    }
}
