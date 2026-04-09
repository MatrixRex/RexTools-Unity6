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
        private int cachedGroupCount = 0;
        private string cachedFirstGroupName = "";

        public EditorTab(BatchMaterialEditorWindow window, VisualElement container)
        {
            this.window = window;
            ui = new EditorUI(container, window.EditorPreset);

            ui.BtnAddGroup.clicked += () => { 
                window.PropertyGroups.Add(new PropertyGroup()); 
                RefreshGroupsUI(); 
                EditorUtility.SetDirty(window.EditorPreset);
            };
        }

        public void SetDisplay(bool isVisible)
        {
            ui.Root.style.display = isVisible ? DisplayStyle.Flex : DisplayStyle.None;
            if (isVisible) RefreshGroupsUI();
        }

        public void OnInspectorUpdate()
        {
            // Detect if the preset data was changed (e.g. by applying a Unity Preset)
            if (window.EditorPreset != null)
            {
                if (window.PropertyGroups.Count != cachedGroupCount || 
                    (window.PropertyGroups.Count > 0 && window.PropertyGroups[0].groupName != cachedFirstGroupName))
                {
                    cachedGroupCount = window.PropertyGroups.Count;
                    if (cachedGroupCount > 0) cachedFirstGroupName = window.PropertyGroups[0].groupName;
                    
                    RefreshGroupsUI();
                }
            }
        }

        public void RefreshGroupsUI()
        {
            ui.GroupsList.Clear();
            if (window.PropertyGroups == null) return;

            cachedGroupCount = window.PropertyGroups.Count;
            if (cachedGroupCount > 0) cachedFirstGroupName = window.PropertyGroups[0].groupName;

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
                        EditorUtility.SetDirty(window.EditorPreset);
                    },
                    onDelete: () => {
                        window.PropertyGroups.Remove(group);
                        RefreshGroupsUI();
                        EditorUtility.SetDirty(window.EditorPreset);
                    },
                    onApplyValue: (g) => {
                        ApplyValueToMaterials(g);
                        EditorUtility.SetDirty(window.EditorPreset);
                    },
                    onAddMaterial: () => {
                        string lastPropName = "_BaseColor";
                        if (group.materials.Count > 0) lastPropName = group.materials[group.materials.Count - 1].propertyName;
                        group.materials.Add(new MaterialEntry() { propertyName = lastPropName });
                        EditorUtility.SetDirty(window.EditorPreset);
                    },
                    onDropMaterials: (List<Material> droppedMats) => {
                        string lastPropName = "_BaseColor";
                        if (group.materials.Count > 0) lastPropName = group.materials[group.materials.Count - 1].propertyName;
                        foreach (var mat in droppedMats) {
                            if (!group.materials.Exists(entry => entry.material == mat)) {
                                group.materials.Add(new MaterialEntry() { material = mat, propertyName = lastPropName });
                            }
                        }
                        EditorUtility.SetDirty(window.EditorPreset);
                    },
                    refreshRoot: () => RefreshGroupsUI()
                );
                ui.GroupsList.Add(groupElement);
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
