using UnityEngine;
using System.Collections.Generic;

namespace RexTools.BatchMaterialEditor
{
    [CreateAssetMenu(fileName = "MaterialConverterSettings", menuName = "RexTools/Internal/MaterialConverterSettings")]
    public class MaterialConverterPreset : ScriptableObject
    {
        public Shader sourceShader;
        public Shader targetShader;
        public Material sourcePreviewMat;
        public Material targetPreviewMat;

        // Path storage for fallback/missing asset detection
        public string sourceShaderPath;
        public string targetShaderPath;
        public string sourceMatPath;
        public string targetMatPath;

        public string sourceShaderName;
        public string targetShaderName;

        public List<PropertyPair> propertyPairs = new List<PropertyPair>();
    }

    [System.Serializable]
    public class PropertyPair
    {
        public string sourceProperty;
        public string targetProperty;
        public int propertyType;
        public int selectedIndex; // Added for UI state persistence
    }
}
