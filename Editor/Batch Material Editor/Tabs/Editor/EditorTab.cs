using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace RexTools.BatchMaterialEditor.Editor.Tabs
{
    public class EditorTab
    {
        private EditorUI ui;
        private BatchMaterialEditorWindow window;

        public EditorTab(BatchMaterialEditorWindow window, VisualElement container)
        {
            this.window = window;
            ui = new EditorUI(container);

            ui.BtnAddGroup.clicked += () => { 
                window.PropertyGroups.Add(new PropertyGroup()); 
                RefreshGroupsUI(); 
            };
            ui.BtnSave.clicked += SaveData;
            ui.BtnLoad.clicked += LoadData;
        }

        public void SetDisplay(bool isVisible)
        {
            ui.Root.style.display = isVisible ? DisplayStyle.Flex : DisplayStyle.None;
            if (isVisible) RefreshGroupsUI();
        }

        public void RefreshGroupsUI()
        {
            ui.GroupsList.Clear();
            foreach (var group in window.PropertyGroups)
            {
                var groupElement = ui.CreateGroupElement(
                    group,
                    onDuplicate: () => {
                        var newGroup = group.Clone();
                        int index = window.PropertyGroups.IndexOf(group);
                        if (index >= 0) window.PropertyGroups.Insert(index + 1, newGroup);
                        else window.PropertyGroups.Add(newGroup);
                        RefreshGroupsUI();
                    },
                    onDelete: () => {
                        window.PropertyGroups.Remove(group);
                        RefreshGroupsUI();
                    },
                    onApplyValue: (g) => ApplyValueToMaterials(g),
                    onAddMaterial: () => {
                        string lastPropName = "_BaseColor";
                        if (group.materials.Count > 0) lastPropName = group.materials[group.materials.Count - 1].propertyName;
                        group.materials.Add(new MaterialEntry() { propertyName = lastPropName });
                    },
                    refreshRoot: () => RefreshGroupsUI()
                );
                ui.GroupsList.Add(groupElement);
            }
        }

        private void SaveData()
        {
            string path = EditorUtility.SaveFilePanelInProject("Save Material Groups", "BatchMatGroups", "asset", "Save property groups to an asset file.");
            if (string.IsNullOrEmpty(path)) return;
            BatchMaterialData data = ScriptableObject.CreateInstance<BatchMaterialData>();
            data.propertyGroups = new List<PropertyGroup>();
            foreach (var g in window.PropertyGroups) data.propertyGroups.Add(g.Clone());
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
                window.PropertyGroups.Clear();
                foreach (var g in data.propertyGroups) window.PropertyGroups.Add(g.Clone());
                RefreshGroupsUI();
            }
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
    }
}
