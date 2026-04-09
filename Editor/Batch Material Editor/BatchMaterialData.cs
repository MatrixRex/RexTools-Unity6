using System.Collections.Generic;
using UnityEngine;

namespace RexTools.BatchMaterialEditor.Editor
{
    public class BatchMaterialData : ScriptableObject
    {
        public List<PropertyGroup> propertyGroups = new List<PropertyGroup>();
    }
}
