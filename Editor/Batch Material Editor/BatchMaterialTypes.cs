using System.Collections.Generic;
using UnityEngine;

namespace RexTools.BatchMaterialEditor.Editor
{
    public enum MatPropType { Float, Color, Vector, Texture }

    [System.Serializable]
    public class MaterialEntry
    {
        public Material material;
        public string propertyName = "None";
    }

    [System.Serializable]
    public class PropertyGroup
    {
        public string groupName = "New Group";
        public MatPropType propertyType = MatPropType.Color;
        public List<MaterialEntry> materials = new List<MaterialEntry>();

        public Color colorVal = Color.white;
        public float floatVal = 0f;
        public Vector4 vectorVal = Vector4.zero;
        public Texture textureVal = null;
        public bool isExpanded = true;

        public PropertyGroup Clone()
        {
            var clone = new PropertyGroup
            {
                groupName = this.groupName + " (Copy)",
                propertyType = this.propertyType,
                colorVal = this.colorVal,
                floatVal = this.floatVal,
                vectorVal = this.vectorVal,
                textureVal = this.textureVal,
                isExpanded = this.isExpanded
            };
            foreach (var matEntry in this.materials)
            {
                clone.materials.Add(new MaterialEntry { material = matEntry.material, propertyName = matEntry.propertyName });
            }
            return clone;
        }
    }

    public enum ReplaceMode { Scene, Prefab, NewPrefab }

    public class ConvPropertyMapping
    {
        public string sourcePropName;
        public string sourcePropDesc;
        public MatPropType type;
        public string targetPropName = "None";
        public string[] targetOptions = new string[] { "None" };
        public string[] targetDisplayOptions = new string[] { "None" };
        public int selectedIndex = 0;
        public bool isValid = false;
    }
}
