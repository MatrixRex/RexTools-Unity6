using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace RexTools.BatchMaterialEditor.Editor.Tabs
{
    public class ReplacerTab
    {
        private ReplacerUI ui;
        private BatchMaterialEditorWindow window;

        private Material findMat;
        private Material replaceMat;
        private ReplaceMode replaceMode = ReplaceMode.Search;
        private List<UnityEngine.Material> manualMaterials = new List<UnityEngine.Material>();
        private Dictionary<UnityEngine.Material, List<GameObject>> affectedGroups = new Dictionary<UnityEngine.Material, List<GameObject>>();
        
        private bool isManualExpanded = true;
        private bool isAffectedExpanded = true;

        public ReplacerTab(BatchMaterialEditorWindow window, VisualElement container)
        {
            this.window = window;
            ui = new ReplacerUI(container);

            ui.FindMatField.RegisterValueChangedCallback(evt => { findMat = (Material)evt.newValue; UpdateConvertButtonState(); });
            ui.ReplaceMatField.RegisterValueChangedCallback(evt => { replaceMat = (Material)evt.newValue; UpdateConvertButtonState(); });
            
            ui.ModeField.RegisterValueChangedCallback(evt => { 
                replaceMode = (ReplaceMode)evt.newValue; 
                UpdateModeUI();
                affectedGroups.Clear(); 
                UpdateConvertButtonState();
                RefreshAffectedListUI(); 
            });

            ui.BtnScanReplace.clicked += InitReplaceScan;
            ui.BtnConvert.clicked += ExecuteReplace;
            ui.BtnAddManualMat.clicked += () => {
                manualMaterials.Add(null);
                UpdateConvertButtonState();
                RefreshManualListUI();
            };
            
            ui.BtnCollapseManual.clicked += () => {
                isManualExpanded = !isManualExpanded;
                UpdateCollapsibles();
            };
            
            ui.BtnCollapseAffected.clicked += () => {
                isAffectedExpanded = !isAffectedExpanded;
                UpdateCollapsibles();
            };

            SetupDragAndDrop();
            UpdateModeUI();
            UpdateCollapsibles();
            RefreshManualListUI();
        }

        private void UpdateCollapsibles()
        {
            ui.ManualMatList.style.display = isManualExpanded ? DisplayStyle.Flex : DisplayStyle.None;
            UpdateLabels();
            
            ui.AffectedListView.style.display = isAffectedExpanded ? DisplayStyle.Flex : DisplayStyle.None;
            UpdateLabels();
        }

        private void UpdateLabels()
        {
            ui.BtnCollapseManual.text = (isManualExpanded ? "▼" : "▶") + $" MANUAL MATERIAL LIST ({manualMaterials.Count})";
            
            int totalAffected = 0;
            foreach(var list in affectedGroups.Values) totalAffected += list.Count;
            ui.BtnCollapseAffected.text = (isAffectedExpanded ? "▼" : "▶") + $" AFFECTED OBJECTS ({totalAffected})";
        }

        private void UpdateModeUI()
        {
            bool isSearch = (replaceMode == ReplaceMode.Search);
            ui.SearchModeSection.EnableInClassList("rex-hidden", !isSearch);
            ui.ManualModeSection.EnableInClassList("rex-hidden", isSearch);
        }

        private void SetupDragAndDrop()
        {
            ui.ManualModeSection.RegisterCallback<DragUpdatedEvent>(evt => {
                if (DragAndDrop.objectReferences.Length > 0) {
                    DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
                    evt.StopPropagation();
                }
            });

            ui.ManualModeSection.RegisterCallback<DragPerformEvent>(evt => {
                DragAndDrop.AcceptDrag();
                foreach (var obj in DragAndDrop.objectReferences) {
                    if (obj is Material mat) {
                        AddManualMaterial(mat);
                    } else if (obj is GameObject go) {
                        var renderers = go.GetComponentsInChildren<Renderer>(true);
                        foreach (var r in renderers) {
                            foreach (var m in r.sharedMaterials) if (m != null) AddManualMaterial(m);
                        }
                    }
                }
                RefreshManualListUI();
                evt.StopPropagation();
            });
        }

        private void AddManualMaterial(Material mat)
        {
            if (!manualMaterials.Contains(mat)) manualMaterials.Add(mat);
        }

        private void RefreshManualListUI()
        {
            ui.PopulateManualMaterials(manualMaterials, (idx) => {
                manualMaterials.RemoveAt(idx);
                RefreshManualListUI();
            });
            UpdateLabels();
        }

        public void SetDisplay(bool isVisible)
        {
            ui.Root.style.display = isVisible ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private void UpdateConvertButtonState()
        {
            int total = 0;
            foreach (var l in affectedGroups.Values) total += l.Count;
            ui.BtnConvert.SetEnabled(total > 0 && replaceMat != null);
        }

        private void InitReplaceScan()
        {
            affectedGroups.Clear();
            
            var renderers = UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include, FindObjectsSortMode.None);

            if (replaceMode == ReplaceMode.Search)
            {
                if (findMat == null) { 
                    EditorUtility.DisplayDialog("Error", "Assign a Find Material.", "OK"); 
                    UpdateConvertButtonState();
                    return; 
                }
                var list = new List<GameObject>();
                foreach (var r in renderers) {
                    if (Array.IndexOf(r.sharedMaterials, findMat) != -1) list.Add(r.gameObject);
                }
                if (list.Count > 0) affectedGroups[findMat] = list;
            }
            else // Manual mode
            {
                if (manualMaterials.Count == 0 || (manualMaterials.Count == 1 && manualMaterials[0] == null)) {
                    EditorUtility.DisplayDialog("Error", "Add at least one material to the manual list.", "OK");
                    UpdateConvertButtonState();
                    return;
                }
                foreach (var r in renderers) {
                    foreach (var m in r.sharedMaterials) {
                        if (m != null && manualMaterials.Contains(m)) {
                            if (!affectedGroups.ContainsKey(m)) affectedGroups[m] = new List<GameObject>();
                            if (!affectedGroups[m].Contains(r.gameObject)) affectedGroups[m].Add(r.gameObject);
                        }
                    }
                }
            }
            
            UpdateConvertButtonState();
            RefreshAffectedListUI();
        }

        private void RefreshAffectedListUI()
        {
            ui.PopulateAffectedList(affectedGroups, RemoveAffectedGroup);
            UpdateLabels();
        }

        private void RemoveAffectedGroup(Material mat)
        {
            if (affectedGroups.ContainsKey(mat))
            {
                affectedGroups.Remove(mat);
            }
            UpdateConvertButtonState();
            RefreshAffectedListUI();
        }

        private void ExecuteReplace()
        {
            if (replaceMat == null || affectedGroups.Count == 0) return;
            
            int total = 0;
            foreach(var l in affectedGroups.Values) total += l.Count;
            if (!EditorUtility.DisplayDialog("Confirm", $"Replace in {total} items?", "Yes", "Cancel")) return;

            // Collect unique renderers to avoid redundant updates
            HashSet<Renderer> uniqueRenderers = new HashSet<Renderer>();
            foreach(var list in affectedGroups.Values) {
                foreach(var go in list) {
                    if (go.TryGetComponent<Renderer>(out var r)) uniqueRenderers.Add(r);
                }
            }

            foreach (var r in uniqueRenderers) 
            {
                var mats = r.sharedMaterials; 
                bool changed = false;
                for (int i = 0; i < mats.Length; i++) 
                {
                    if (replaceMode == ReplaceMode.Search) {
                        if (mats[i] == findMat) { mats[i] = replaceMat; changed = true; }
                    } else {
                        if (mats[i] != null && manualMaterials.Contains(mats[i])) { mats[i] = replaceMat; changed = true; }
                    }
                }
                if (changed) 
                { 
                    Undo.RecordObject(r, "Replace Material"); 
                    r.sharedMaterials = mats; 
                    EditorUtility.SetDirty(r); 
                }
            }
            AssetDatabase.SaveAssets(); 
            InitReplaceScan();
        }
    }
}
