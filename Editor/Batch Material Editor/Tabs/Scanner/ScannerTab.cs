using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace RexTools.BatchMaterialEditor.Editor.Tabs
{
    public class ScannerTab
    {
        private ScannerUI ui;
        private BatchMaterialEditorWindow window;

        public ScannerTab(BatchMaterialEditorWindow window, VisualElement container)
        {
            this.window = window;
            ui = new ScannerUI(container);

            ui.BtnScan.clicked += PerformScan;
            ui.BtnCreateGroupFromSelection.clicked += CreateGroupFromSelection;
        }

        public void SetDisplay(bool isVisible)
        {
            ui.Root.style.display = isVisible ? DisplayStyle.Flex : DisplayStyle.None;
        }

        public void PerformScan()
        {
            ui.ScannerList.Clear();
            window.SelectedMaterials.Clear();
            UpdateCreateGroupButtonVisibility();

            HashSet<Material> foundMaterials = new HashSet<Material>();
            var renderers = Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var r in renderers)
            {
                if (r.sharedMaterials == null) continue;
                foreach (var mat in r.sharedMaterials) if (mat != null) foundMaterials.Add(mat);
            }

            if (foundMaterials.Count == 0)
            {
                ui.ScannerList.Add(new Label("No materials found in current scenes.") { style = { marginTop = 20, alignSelf = Align.Center } });
                return;
            }

            foreach (var mat in foundMaterials)
            {
                var row = new VisualElement();
                row.AddToClassList("rex-result-item");

                var toggle = new Toggle { value = false, style = { marginRight = 5 } };
                toggle.RegisterValueChangedCallback(evt => {
                    if (evt.newValue) window.SelectedMaterials.Add(mat);
                    else window.SelectedMaterials.Remove(mat);
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
                ui.ScannerList.Add(row);
            }
        }

        private void UpdateCreateGroupButtonVisibility()
        {
            ui.BtnCreateGroupFromSelection.style.display = window.SelectedMaterials.Count > 0 ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private void CreateGroupFromSelection()
        {
            if (window.SelectedMaterials.Count == 0) return;
            var newGroup = new PropertyGroup { groupName = "Group " + (window.PropertyGroups.Count + 1), isExpanded = true };
            foreach (var mat in window.SelectedMaterials) newGroup.materials.Add(new MaterialEntry { material = mat, propertyName = "_BaseColor" });
            window.PropertyGroups.Add(newGroup);
            
            // Re-sync UI state on the main window
            window.RefreshGroupsUI();
            window.SwitchToTab(1);
            
            window.SelectedMaterials.Clear();
            UpdateCreateGroupButtonVisibility();
            PerformScan();
        }
    }
}
