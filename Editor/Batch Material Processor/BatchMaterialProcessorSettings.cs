using System.Collections.Generic;
using UnityEngine;

namespace RexTools.BatchMaterialProcessor.Editor
{
    public class BatchMaterialProcessorSettings : ScriptableObject
    {
        public List<Material> materials = new List<Material>();
        public Shader targetShader;
        public string searchFolderPath = "Assets";
        public bool recursiveSearch = false;
        public List<SuffixMapping> suffixMappings = new List<SuffixMapping>();
        public List<MaterialMatchResult> matchResults = new List<MaterialMatchResult>();
    }
}
