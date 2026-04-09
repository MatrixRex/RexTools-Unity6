using System.Collections.Generic;
using UnityEngine;

namespace RexTools.BatchMaterialEditor.Editor
{
    [CreateAssetMenu(fileName = "MaterialEditorPreset", menuName = "RexTools/Internal/MaterialEditorPreset")]
    public class MaterialEditorPreset : ScriptableObject
    {
        public List<PropertyGroup> propertyGroups = new List<PropertyGroup>();
    }
}
