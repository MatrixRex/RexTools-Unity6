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
        public Button BtnSave { get; private set; }
        public Button BtnLoad { get; private set; }
        public ScrollView GroupsList { get; private set; }

        public EditorUI(VisualElement container)
        {
            Root = new VisualElement { style = { flexGrow = 1, display = DisplayStyle.None } };

            var editorToolbar = new VisualElement();
            editorToolbar.AddToClassList("rex-row");
            editorToolbar.style.marginBottom = 10;

            BtnAddGroup = new Button { text = "Add Property Group" };
            BtnAddGroup.AddToClassList("rex-flex-grow");

            BtnSave = new Button { text = "Save", tooltip = "Save groups to .asset" };
            BtnLoad = new Button { text = "Load", tooltip = "Load groups from .asset" };

            editorToolbar.Add(BtnAddGroup);
            editorToolbar.Add(BtnSave);
            editorToolbar.Add(BtnLoad);

            Root.Add(editorToolbar);

            GroupsList = new ScrollView { style = { flexGrow = 1 } };
            Root.Add(GroupsList);

            container.Add(Root);
        }

        // Factory function for dynamic elements
        public VisualElement CreateGroupElement(PropertyGroup group, System.Action onDuplicate, System.Action onDelete, System.Action<PropertyGroup> onApplyValue, System.Action onAddMaterial, System.Action refreshRoot)
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

            var dupBtn = new Button(onDuplicate) { text = "Copy", tooltip = "Duplicate Group" };
            dupBtn.AddToClassList("rex-button-small");

            var deleteBtn = new Button(onDelete) { text = "X", tooltip = "Delete Group" };
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
            })
            {
                text = group.isExpanded ? "▼ MATERIALS" : "▶ MATERIALS",
                style = { unityFontStyleAndWeight = FontStyle.Bold, backgroundColor = Color.clear, paddingLeft = 0, fontSize = 10, color = new Color(0.6f, 0.6f, 0.6f) }
            };
            collapseBtn.style.borderTopWidth = 0; collapseBtn.style.borderBottomWidth = 0; collapseBtn.style.borderLeftWidth = 0; collapseBtn.style.borderRightWidth = 0;

            var addMatBtn = new Button(onAddMaterial) { text = "+ ADD MATERIAL" };
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
            colorField.RegisterValueChangedCallback(evt => { group.colorVal = evt.newValue; onApplyValue?.Invoke(group); });
            floatField.RegisterValueChangedCallback(evt => { group.floatVal = evt.newValue; onApplyValue?.Invoke(group); });
            vectorField.RegisterValueChangedCallback(evt => { group.vectorVal = evt.newValue; onApplyValue?.Invoke(group); });
            texField.RegisterValueChangedCallback(evt => { group.textureVal = (Texture)evt.newValue; onApplyValue?.Invoke(group); });

            System.Action refreshMatListUI = null;
            refreshMatListUI = () => {
                matList.Clear();
                for (int i = 0; i < group.materials.Count; i++)
                {
                    int index = i;
                    var row = new VisualElement { style = { flexDirection = FlexDirection.Row, marginTop = 2 } };
                    var matField = new ObjectField { objectType = typeof(Material), value = group.materials[index].material, style = { flexGrow = 1, flexShrink = 1, minWidth = 0 } };
                    var propNameField = new TextField { value = group.materials[index].propertyName, tooltip = "Property Name", style = { width = 120, marginLeft = 5, flexShrink = 0 } };
                    var errIcon = new VisualElement { style = { width = 16, height = 16, marginLeft = 4, marginRight = 4, display = DisplayStyle.None, backgroundImage = (Texture2D)EditorGUIUtility.IconContent("console.erroricon.sml").image } };
                    
                    var rmvBtn = new Button(() => { group.materials.RemoveAt(index); refreshMatListUI(); }) { text = "-" };
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

            // Bind the add material logic to re-trigger internal update
            addMatBtn.clicked += refreshMatListUI;

            updateVisibility(); 
            refreshMatListUI();
            return box;
        }
    }
}
