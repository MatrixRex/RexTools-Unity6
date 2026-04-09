using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace RexTools.BatchMaterialEditor.Editor.Tabs
{
    public class ReplaceTab
    {
        private ReplaceUI ui;
        private BatchMaterialEditorWindow window;

        private Material findMat;
        private Material replaceMat;
        private ReplaceMode replaceMode = ReplaceMode.Scene;
        private List<UnityEngine.Object> affectedObjects = new List<UnityEngine.Object>();

        public ReplaceTab(BatchMaterialEditorWindow window, VisualElement container)
        {
            this.window = window;
            ui = new ReplaceUI(container);

            ui.FindMatField.RegisterValueChangedCallback(evt => findMat = (Material)evt.newValue);
            ui.ReplaceMatField.RegisterValueChangedCallback(evt => replaceMat = (Material)evt.newValue);
            ui.ModeField.RegisterValueChangedCallback(evt => { 
                replaceMode = (ReplaceMode)evt.newValue; 
                affectedObjects.Clear(); 
                RefreshAffectedListUI(); 
            });

            ui.BtnScanReplace.clicked += InitReplaceScan;
            ui.BtnConvert.clicked += ExecuteReplace;
        }

        public void SetDisplay(bool isVisible)
        {
            ui.Root.style.display = isVisible ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private void InitReplaceScan()
        {
            affectedObjects.Clear();
            if (findMat == null) { 
                EditorUtility.DisplayDialog("Error", "Assign a Find Material.", "OK"); 
                return; 
            }
            var renderers = UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var r in renderers)
            {
                if (Array.IndexOf(r.sharedMaterials, findMat) != -1) affectedObjects.Add(r.gameObject);
            }
            
            ui.BtnConvert.SetEnabled(affectedObjects.Count > 0);
            RefreshAffectedListUI();
        }

        private void RefreshAffectedListUI()
        {
            ui.PopulateAffectedList(affectedObjects, RemoveAffectedObject);
        }

        private void RemoveAffectedObject(int index)
        {
            affectedObjects.RemoveAt(index);
            RefreshAffectedListUI();
        }

        private void ExecuteReplace()
        {
            if (findMat == null || replaceMat == null || affectedObjects.Count == 0) return;
            if (!EditorUtility.DisplayDialog("Confirm", $"Replace in {affectedObjects.Count} items?", "Yes", "Cancel")) return;
            
            foreach (var obj in affectedObjects) 
            {
                if (obj is GameObject go && go.TryGetComponent<Renderer>(out var r)) 
                {
                    var mats = r.sharedMaterials; 
                    bool changed = false;
                    for (int i = 0; i < mats.Length; i++) 
                    {
                        if (mats[i] == findMat) 
                        { 
                            mats[i] = replaceMat; 
                            changed = true; 
                        }
                    }
                    if (changed) 
                    { 
                        Undo.RecordObject(r, "Replace Material"); 
                        r.sharedMaterials = mats; 
                        EditorUtility.SetDirty(r); 
                    }
                }
            }
            AssetDatabase.SaveAssets(); 
            InitReplaceScan();
        }
    }
}
