using System.Collections.Generic;
using UnityEngine;

namespace RexTools.BatchMaterialProcessor.Editor
{
    [System.Serializable]
    public class SuffixMapping
    {
        public string propertyName;
        public string propertyDescription;
        public string suffixes; // Comma-separated
    }

    [System.Serializable]
    public class PropertyMatchEntry
    {
        public string propertyName;
        public string propertyDescription;
        public Texture matchedTexture;
        public Texture overrideTexture;
        public bool isSelected = true;
    }

    [System.Serializable]
    public class MaterialMatchResult
    {
        public Material material;
        public List<PropertyMatchEntry> propertyMatches = new List<PropertyMatchEntry>();
        public bool isExpanded = true;
    }
}
