using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using RexTools.BatchMaterialEditor;

namespace RexTools.BatchMaterialEditor.Editor.Tabs
{
    public class ConverterTab
    {
        private ConverterUI ui;
        private BatchMaterialEditorWindow window;

        private MaterialConverterPreset activeSwitcherSettings;
        private SerializedObject serializedSwitcherSettings;

        private Shader sourceShader;
        private Shader targetShader;
        private Material sourcePreviewMat;
        private Material targetPreviewMat;

        private List<ConvPropertyMapping> convMappings = new List<ConvPropertyMapping>();

        public ConverterTab(BatchMaterialEditorWindow window, VisualElement container)
        {
            this.window = window;

            // Init Settings
            if (activeSwitcherSettings == null)
            {
                activeSwitcherSettings = ScriptableObject.CreateInstance<MaterialConverterPreset>();
                activeSwitcherSettings.name = "MaterialConverterSettings_Runtime";
            }
            serializedSwitcherSettings = new SerializedObject(activeSwitcherSettings);

            ui = new ConverterUI(container, activeSwitcherSettings);

            ui.SourceDrop.RegisterValueChangedCallback(evt => {
                var obj = evt.newValue;
                if (obj is Material mat) {
                    activeSwitcherSettings.sourcePreviewMat = mat;
                    activeSwitcherSettings.sourceShader = mat.shader;
                    activeSwitcherSettings.sourceMatPath = AssetDatabase.GetAssetPath(mat);
                    activeSwitcherSettings.sourceShaderPath = AssetDatabase.GetAssetPath(mat.shader);
                    activeSwitcherSettings.sourceShaderName = mat.shader.name;
                } else if (obj is Shader shader) {
                    activeSwitcherSettings.sourcePreviewMat = null;
                    activeSwitcherSettings.sourceShader = shader;
                    activeSwitcherSettings.sourceMatPath = "";
                    activeSwitcherSettings.sourceShaderPath = AssetDatabase.GetAssetPath(shader);
                    activeSwitcherSettings.sourceShaderName = shader.name;
                } else {
                    activeSwitcherSettings.sourcePreviewMat = null;
                    activeSwitcherSettings.sourceShader = null;
                    activeSwitcherSettings.sourceMatPath = "";
                    activeSwitcherSettings.sourceShaderPath = "";
                    activeSwitcherSettings.sourceShaderName = "";
                }
                sourceShader = activeSwitcherSettings.sourceShader;
                sourcePreviewMat = activeSwitcherSettings.sourcePreviewMat;
                EditorUtility.SetDirty(activeSwitcherSettings);
            });

            ui.TargetDrop.RegisterValueChangedCallback(evt => {
                var obj = evt.newValue;
                if (obj is Material mat) {
                    activeSwitcherSettings.targetPreviewMat = mat;
                    activeSwitcherSettings.targetShader = mat.shader;
                    activeSwitcherSettings.targetMatPath = AssetDatabase.GetAssetPath(mat);
                    activeSwitcherSettings.targetShaderPath = AssetDatabase.GetAssetPath(mat.shader);
                    activeSwitcherSettings.targetShaderName = mat.shader.name;
                } else if (obj is Shader shader) {
                    activeSwitcherSettings.targetPreviewMat = null;
                    activeSwitcherSettings.targetShader = shader;
                    activeSwitcherSettings.targetMatPath = "";
                    activeSwitcherSettings.targetShaderPath = AssetDatabase.GetAssetPath(shader);
                    activeSwitcherSettings.targetShaderName = shader.name;
                } else {
                    activeSwitcherSettings.targetPreviewMat = null;
                    activeSwitcherSettings.targetShader = null;
                    activeSwitcherSettings.targetMatPath = "";
                    activeSwitcherSettings.targetShaderPath = "";
                    activeSwitcherSettings.targetShaderName = "";
                }
                targetShader = activeSwitcherSettings.targetShader;
                targetPreviewMat = activeSwitcherSettings.targetPreviewMat;
                EditorUtility.SetDirty(activeSwitcherSettings);
            });

            ui.BtnLoadProps.clicked += () => LoadMappings(true, true);
            ui.BtnPerformConverter.clicked += PerformConverterConversion;
        }

        public void SetDisplay(bool isVisible)
        {
            ui.Root.style.display = isVisible ? DisplayStyle.Flex : DisplayStyle.None;
        }

        public void OnInspectorUpdate()
        {
            if (activeSwitcherSettings != null)
            {
                ValidateAndFallbackAssets();

                if (activeSwitcherSettings.sourceShader != sourceShader || activeSwitcherSettings.targetShader != targetShader)
                {
                    sourceShader = activeSwitcherSettings.sourceShader;
                    targetShader = activeSwitcherSettings.targetShader;
                    sourcePreviewMat = activeSwitcherSettings.sourcePreviewMat;
                    targetPreviewMat = activeSwitcherSettings.targetPreviewMat;
                    
                    SyncSettingsToMappings();
                    
                    if (ui.SourceDrop != null) ui.SourceDrop.value = sourcePreviewMat != null ? (UnityEngine.Object)sourcePreviewMat : sourceShader;
                    if (ui.TargetDrop != null) ui.TargetDrop.value = targetPreviewMat != null ? (UnityEngine.Object)targetPreviewMat : targetShader;
                }
            }
        }

        private void ValidateAndFallbackAssets()
        {
            if (activeSwitcherSettings == null) return;
            bool recovered = false;

            // 1. Source Fallback
            if (activeSwitcherSettings.sourcePreviewMat == null && !string.IsNullOrEmpty(activeSwitcherSettings.sourceMatPath))
            {
                activeSwitcherSettings.sourcePreviewMat = AssetDatabase.LoadAssetAtPath<Material>(activeSwitcherSettings.sourceMatPath);
                if (activeSwitcherSettings.sourcePreviewMat != null) recovered = true;
            }

            if (activeSwitcherSettings.sourceShader == null)
            {
                if (!string.IsNullOrEmpty(activeSwitcherSettings.sourceShaderPath))
                {
                    activeSwitcherSettings.sourceShader = AssetDatabase.LoadAssetAtPath<Shader>(activeSwitcherSettings.sourceShaderPath);
                    if (activeSwitcherSettings.sourceShader != null) recovered = true;
                }

                if (activeSwitcherSettings.sourceShader == null && !string.IsNullOrEmpty(activeSwitcherSettings.sourceShaderName))
                {
                    string[] guids = AssetDatabase.FindAssets($"{activeSwitcherSettings.sourceShaderName} t:Shader");
                    if (guids.Length > 0)
                    {
                        activeSwitcherSettings.sourceShader = AssetDatabase.LoadAssetAtPath<Shader>(AssetDatabase.GUIDToAssetPath(guids[0]));
                        if (activeSwitcherSettings.sourceShader != null) recovered = true;
                    }
                }
            }

            // 2. Target Fallback
            if (activeSwitcherSettings.targetPreviewMat == null && !string.IsNullOrEmpty(activeSwitcherSettings.targetMatPath))
            {
                activeSwitcherSettings.targetPreviewMat = AssetDatabase.LoadAssetAtPath<Material>(activeSwitcherSettings.targetMatPath);
                if (activeSwitcherSettings.targetPreviewMat != null) recovered = true;
            }

            if (activeSwitcherSettings.targetShader == null)
            {
                if (!string.IsNullOrEmpty(activeSwitcherSettings.targetShaderPath))
                {
                    activeSwitcherSettings.targetShader = AssetDatabase.LoadAssetAtPath<Shader>(activeSwitcherSettings.targetShaderPath);
                    if (activeSwitcherSettings.targetShader != null) recovered = true;
                }

                if (activeSwitcherSettings.targetShader == null && !string.IsNullOrEmpty(activeSwitcherSettings.targetShaderName))
                {
                    string[] guids = AssetDatabase.FindAssets($"{activeSwitcherSettings.targetShaderName} t:Shader");
                    if (guids.Length > 0)
                    {
                        activeSwitcherSettings.targetShader = AssetDatabase.LoadAssetAtPath<Shader>(AssetDatabase.GUIDToAssetPath(guids[0]));
                        if (activeSwitcherSettings.targetShader != null) recovered = true;
                    }
                }
            }

            bool shadersValid = activeSwitcherSettings.sourceShader != null && activeSwitcherSettings.targetShader != null;
            if (ui.BtnPerformConverter != null)
            {
                ui.BtnPerformConverter.SetEnabled(shadersValid);
                ui.BtnPerformConverter.text = shadersValid ? "CONVERT SELECTED MATERIALS" : "MISSING SHADERS (CHECK SETTINGS)";
            }

            if (recovered)
            {
                EditorUtility.SetDirty(activeSwitcherSettings);
            }
        }

        private void SyncSettingsToMappings()
        {
            if (activeSwitcherSettings == null) return;
            
            LoadMappings(false, false); 

            if (activeSwitcherSettings.propertyPairs.Count == 0) return;
            
            foreach (var pair in activeSwitcherSettings.propertyPairs)
            {
                var m = convMappings.FirstOrDefault(x => x.sourcePropName == pair.sourceProperty);
                if (m != null)
                {
                    m.targetPropName = pair.targetProperty;
                    m.selectedIndex = pair.selectedIndex;
                    m.isValid = m.targetPropName != "None";
                }
            }
            RefreshMappingsUI();
        }

        private void LoadMappings(bool showDialogOnError, bool runSmartMatch = true)
        {
            convMappings.Clear();

            Shader sShader = sourceShader != null ? sourceShader : (sourcePreviewMat != null ? sourcePreviewMat.shader : null);
            Shader tShader = targetShader != null ? targetShader : (targetPreviewMat != null ? targetPreviewMat.shader : null);

            if (sShader == null || tShader == null) {
                if (showDialogOnError) EditorUtility.DisplayDialog("Error", "Source and Target shaders must be assigned.", "OK");
                ui.SwitcherMappingList.Clear();
                return;
            }

            int propCount = ShaderUtil.GetPropertyCount(sShader);
            int targetPropCount = ShaderUtil.GetPropertyCount(tShader);

            for (int i = 0; i < propCount; i++) {
                if (ShaderUtil.IsShaderPropertyHidden(sShader, i)) continue;

                string name = ShaderUtil.GetPropertyName(sShader, i);
                string desc = ShaderUtil.GetPropertyDescription(sShader, i);
                var type = GetConvType(ShaderUtil.GetPropertyType(sShader, i));

                var options = new List<string> { "None" };
                var displayOptions = new List<string> { "None" };

                for (int j = 0; j < targetPropCount; j++) {
                    if (GetConvType(ShaderUtil.GetPropertyType(tShader, j)) == type) {
                        string tName = ShaderUtil.GetPropertyName(tShader, j);
                        string tDesc = ShaderUtil.GetPropertyDescription(tShader, j);
                        options.Add(tName);
                        displayOptions.Add($"{tDesc} ({tName})");
                    }
                }

                convMappings.Add(new ConvPropertyMapping {
                    sourcePropName = name,
                    sourcePropDesc = desc,
                    type = type,
                    targetOptions = options.ToArray(),
                    targetDisplayOptions = displayOptions.ToArray(),
                    targetPropName = "None",
                    selectedIndex = 0
                });
            }
            
            convMappings = convMappings.OrderBy(m => m.type).ThenBy(m => m.sourcePropDesc).ToList();

            if (runSmartMatch) {
                SmartMatch();
            } else {
                RefreshMappingsUI();
            }
        }

        private void RefreshMappingsUI()
        {
            ui.PopulateMappings(convMappings, sourcePreviewMat, SyncMappingsToSettings);
        }

        private void SyncMappingsToSettings()
        {
            if (activeSwitcherSettings == null) return;
            
            Undo.RecordObject(activeSwitcherSettings, "Sync Material Converter Mappings");
            activeSwitcherSettings.propertyPairs.Clear();
            foreach (var m in convMappings)
            {
                if (m.isValid)
                {
                    activeSwitcherSettings.propertyPairs.Add(new PropertyPair 
                    { 
                        sourceProperty = m.sourcePropName, 
                        targetProperty = m.targetPropName, 
                        propertyType = (int)m.type,
                        selectedIndex = m.selectedIndex
                    });
                }
            }
            EditorUtility.SetDirty(activeSwitcherSettings);
        }

        private void SmartMatch()
        {
            foreach (var m in convMappings) {
                string bestTarget = null;
                List<int> exactVisualMatches = new List<int>();
                List<int> fuzzyVisualMatches = new List<int>();

                if (!string.IsNullOrEmpty(m.sourcePropDesc)) {
                    for (int i = 1; i < m.targetDisplayOptions.Length; i++) {
                        string targetDesc = m.targetDisplayOptions[i].Split('(')[0].Trim();
                        if (string.Equals(targetDesc, m.sourcePropDesc, StringComparison.OrdinalIgnoreCase)) {
                            exactVisualMatches.Add(i);
                        } 
                        else if (targetDesc.IndexOf(m.sourcePropDesc, StringComparison.OrdinalIgnoreCase) >= 0 ||
                                   m.sourcePropDesc.IndexOf(targetDesc, StringComparison.OrdinalIgnoreCase) >= 0) {
                            fuzzyVisualMatches.Add(i);
                        }
                    }
                }

                List<int> activeMatches = exactVisualMatches.Count > 0 ? exactVisualMatches : fuzzyVisualMatches;
                int finalMatchIdx = -1;

                if (activeMatches.Count == 1) {
                    finalMatchIdx = activeMatches[0];
                } else if (activeMatches.Count > 1) {
                    foreach (int idx in activeMatches) {
                        if (m.targetOptions[idx].Equals(m.sourcePropName, StringComparison.OrdinalIgnoreCase)) {
                            finalMatchIdx = idx;
                            break;
                        }
                    }

                    if (finalMatchIdx == -1) {
                        string cleanSource = m.sourcePropName.TrimStart('_');
                        foreach (int idx in activeMatches) {
                            if (m.targetOptions[idx].TrimStart('_').Equals(cleanSource, StringComparison.OrdinalIgnoreCase)) {
                                finalMatchIdx = idx;
                                break;
                            }
                        }
                    }

                    if (finalMatchIdx == -1) {
                        finalMatchIdx = activeMatches[0];
                    }
                }

                if (finalMatchIdx != -1) {
                    bestTarget = m.targetOptions[finalMatchIdx];
                }

                if (bestTarget == null) {
                    bestTarget = m.targetOptions.FirstOrDefault(opt => opt.Equals(m.sourcePropName, StringComparison.OrdinalIgnoreCase));
                }
                
                if (bestTarget == null) {
                    string cleanSource = m.sourcePropName.TrimStart('_');
                    bestTarget = m.targetOptions.FirstOrDefault(opt => opt.TrimStart('_').Equals(cleanSource, StringComparison.OrdinalIgnoreCase));
                }

                if (bestTarget != null) {
                    m.targetPropName = bestTarget;
                    m.selectedIndex = Array.IndexOf(m.targetOptions, bestTarget);
                    m.isValid = true;
                }
            }
            SyncMappingsToSettings();
            RefreshMappingsUI();
        }

        private ConvPropertyType GetConvType(ShaderUtil.ShaderPropertyType type)
        {
            return type switch {
                ShaderUtil.ShaderPropertyType.Color => ConvPropertyType.Color,
                ShaderUtil.ShaderPropertyType.Vector => ConvPropertyType.Vector,
                ShaderUtil.ShaderPropertyType.TexEnv => ConvPropertyType.Texture,
                _ => ConvPropertyType.Float
            };
        }

        private void PerformConverterConversion()
        {
            HashSet<Material> matsToConvert = new HashSet<Material>();

            foreach (var obj in Selection.objects)
            {
                if (obj is Material m) matsToConvert.Add(m);
            }

            foreach (var go in Selection.gameObjects)
            {
                var renderers = go.GetComponentsInChildren<Renderer>(true);
                foreach (var renderer in renderers)
                {
                    foreach (var sharedMat in renderer.sharedMaterials)
                    {
                        if (sharedMat != null) matsToConvert.Add(sharedMat);
                    }
                }
            }

            if (targetShader == null)
            {
                EditorUtility.DisplayDialog("Error", "Target shader not set or missing.", "OK");
                return;
            }

            matsToConvert.RemoveWhere(m => m.shader == targetShader);

            if (matsToConvert.Count == 0)
            {
                EditorUtility.DisplayDialog("Selection Processed", "No materials need conversion (all selected materials already use the target shader or selection is empty).", "OK");
                return;
            }

            if (!EditorUtility.DisplayDialog("Confirm Conversion", $"Are you sure you want to convert {matsToConvert.Count} unique materials to {targetShader.name}?\n\nThis will modify the material files directly.", "Convert", "Cancel"))
            {
                return;
            }

            List<Material> matList = matsToConvert.ToList();
            Undo.RecordObjects(matList.ToArray(), "Batch Switch Shader");

            foreach (var mat in matList) {
                var valueCache = new Dictionary<string, object>();
                var texDataCache = new Dictionary<string, (Vector2 offset, Vector2 scale)>();

                foreach (var m in convMappings) {
                    if (!mat.HasProperty(m.sourcePropName)) continue;
                    
                    switch (m.type) {
                        case ConvPropertyType.Color: valueCache[m.sourcePropName] = mat.GetColor(m.sourcePropName); break;
                        case ConvPropertyType.Float: valueCache[m.sourcePropName] = mat.GetFloat(m.sourcePropName); break;
                        case ConvPropertyType.Vector: valueCache[m.sourcePropName] = mat.GetVector(m.sourcePropName); break;
                        case ConvPropertyType.Texture: 
                            valueCache[m.sourcePropName] = mat.GetTexture(m.sourcePropName);
                            texDataCache[m.sourcePropName] = (mat.GetTextureOffset(m.sourcePropName), mat.GetTextureScale(m.sourcePropName));
                            break;
                    }
                }

                mat.shader = targetShader;

                foreach (var m in convMappings) {
                    if (!m.isValid || !mat.HasProperty(m.targetPropName)) continue;
                    if (!valueCache.ContainsKey(m.sourcePropName)) continue;

                    var val = valueCache[m.sourcePropName];
                    switch (m.type) {
                        case ConvPropertyType.Color: mat.SetColor(m.targetPropName, (Color)val); break;
                        case ConvPropertyType.Float: mat.SetFloat(m.targetPropName, (float)val); break;
                        case ConvPropertyType.Vector: mat.SetVector(m.targetPropName, (Vector4)val); break;
                        case ConvPropertyType.Texture: 
                            mat.SetTexture(m.targetPropName, (Texture)val);
                            if (texDataCache.TryGetValue(m.sourcePropName, out var texData)) {
                                mat.SetTextureOffset(m.targetPropName, texData.offset);
                                mat.SetTextureScale(m.targetPropName, texData.scale);
                            }
                            break;
                    }
                }
                EditorUtility.SetDirty(mat);
            }
            AssetDatabase.SaveAssets();
            EditorUtility.DisplayDialog("Success", $"Converted {matsToConvert.Count} unique materials.", "OK");
        }
    }
}
