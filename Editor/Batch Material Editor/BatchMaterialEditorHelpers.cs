using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace RexTools.BatchMaterialEditor.Editor
{
    public static class BatchMaterialEditorHelpers
    {
        public static (List<string> Names, List<string> DisplayNames) GetShaderProperties(Shader shader, MatPropType? filterType = null)
        {
            var names = new List<string> { "None" };
            var displayNames = new List<string> { "None" };

            if (shader == null) return (names, displayNames);

            int propCount = ShaderUtil.GetPropertyCount(shader);
            for (int i = 0; i < propCount; i++)
            {
                if (ShaderUtil.IsShaderPropertyHidden(shader, i)) continue;

                var type = GetMatPropType(ShaderUtil.GetPropertyType(shader, i));
                
                if (filterType.HasValue && filterType.Value != type) continue;

                string name = ShaderUtil.GetPropertyName(shader, i);
                string desc = ShaderUtil.GetPropertyDescription(shader, i);
                
                names.Add(name);
                displayNames.Add($"{desc} ({name})");
            }

            return (names, displayNames);
        }

        public static MatPropType GetMatPropType(ShaderUtil.ShaderPropertyType type)
        {
            return type switch
            {
                ShaderUtil.ShaderPropertyType.Color => MatPropType.Color,
                ShaderUtil.ShaderPropertyType.Vector => MatPropType.Vector,
                ShaderUtil.ShaderPropertyType.TexEnv => MatPropType.Texture,
                _ => MatPropType.Float
            };
        }
    }
}
