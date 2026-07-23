using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

namespace RexTools.ShaderGraphCustomFunctionSync.Editor
{
    /// <summary>
    /// Adds a context menu item to Unity ShaderGraph when a Custom Function Node is selected,
    /// enabling automatic synchronization of ports/slots with its HLSL file signature.
    /// </summary>
    [InitializeOnLoad]
    public static class ShaderGraphCustomFunctionSyncExtension
    {
        private static Type materialGraphEditWindowType;
        private static Type graphEditorViewType;
        private static Type materialGraphViewType;
        private static Type materialNodeViewType;
        private static Type customFunctionNodeType;
        private static Type slotTypeEnum;
        private static Type modificationScopeEnum;

        // Tracks graph views that already have the contextual menu callback registered
        private static readonly HashSet<int> attachedGraphViews = new HashSet<int>();

        static ShaderGraphCustomFunctionSyncExtension()
        {
            try
            {
                var assembly = Assembly.Load("Unity.ShaderGraph.Editor");
                materialGraphEditWindowType = assembly.GetType("UnityEditor.ShaderGraph.Drawing.MaterialGraphEditWindow");
                graphEditorViewType = assembly.GetType("UnityEditor.ShaderGraph.Drawing.GraphEditorView");
                materialGraphViewType = assembly.GetType("UnityEditor.ShaderGraph.Drawing.MaterialGraphView");
                materialNodeViewType = assembly.GetType("UnityEditor.ShaderGraph.Drawing.MaterialNodeView");
                customFunctionNodeType = assembly.GetType("UnityEditor.ShaderGraph.CustomFunctionNode");
                slotTypeEnum = assembly.GetType("UnityEditor.Graphing.SlotType");
                modificationScopeEnum = assembly.GetType("UnityEditor.Graphing.ModificationScope");

                EditorApplication.update += OnEditorUpdate;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"ShaderGraph Custom Function Sync: Failed to initialize reflection - {e.Message}");
            }
        }

        private static void OnEditorUpdate()
        {
            if (materialGraphEditWindowType == null) return;

            var focused = EditorWindow.focusedWindow;
            if (focused == null || !materialGraphEditWindowType.IsInstanceOfType(focused)) return;

            var windows = Resources.FindObjectsOfTypeAll(materialGraphEditWindowType);
            foreach (var window in windows)
            {
                if (window is EditorWindow editorWindow)
                {
                    AttachContextMenu(editorWindow, window);
                }
            }
        }

        private static void AttachContextMenu(EditorWindow editorWindow, object windowInstance)
        {
            var graphEditorViewField = materialGraphEditWindowType.GetField(
                "m_GraphEditorView", BindingFlags.NonPublic | BindingFlags.Instance);
            if (graphEditorViewField == null) return;

            var graphEditorView = graphEditorViewField.GetValue(windowInstance) as VisualElement;
            if (graphEditorView == null) return;

            var graphViewProperty = graphEditorViewType.GetProperty(
                "graphView", BindingFlags.Public | BindingFlags.Instance);
            if (graphViewProperty == null) return;

            if (graphViewProperty.GetValue(graphEditorView) is GraphView graphView)
            {
                int instanceId = graphView.GetHashCode();
                if (attachedGraphViews.Contains(instanceId)) return;
                attachedGraphViews.Add(instanceId);

                graphView.RegisterCallback<ContextualMenuPopulateEvent>(evt =>
                {
                    OnContextMenuPopulate(evt, graphView);
                });
            }
        }

        private static void OnContextMenuPopulate(ContextualMenuPopulateEvent evt, GraphView graphView)
        {
            if (graphView.selection == null || graphView.selection.Count != 1) return;

            var selected = graphView.selection[0];
            if (materialNodeViewType == null || !materialNodeViewType.IsInstanceOfType(selected)) return;

            var nodeProperty = materialNodeViewType.GetProperty("node", BindingFlags.Public | BindingFlags.Instance);
            if (nodeProperty == null) return;

            var node = nodeProperty.GetValue(selected);
            if (node == null || customFunctionNodeType == null || !customFunctionNodeType.IsInstanceOfType(node)) return;

            evt.menu.AppendSeparator();
            evt.menu.AppendAction("Rex Tools/Sync Ports from HLSL", action =>
            {
                SyncPorts(node, graphView);
            });
        }

        private struct ParsedParameter
        {
            public string Name;
            public string TypeStr;
            public bool IsOutput;
        }

        private static void SyncPorts(object node, GraphView graphView)
        {
            if (customFunctionNodeType == null) return;

            // 1. Verify source type is File (0 = String, 1 = File)
            var sourceTypeProp = customFunctionNodeType.GetProperty("sourceType", BindingFlags.Public | BindingFlags.Instance);
            if (sourceTypeProp == null) return;

            var sourceTypeObj = sourceTypeProp.GetValue(node);
            if (sourceTypeObj == null || sourceTypeObj.ToString() != "File")
            {
                Debug.LogWarning("Rex Tools Sync: Port synchronization is only supported when Custom Function Node is in 'File' mode.");
                return;
            }

            // 2. Retrieve function name
            var functionNameProp = customFunctionNodeType.GetProperty("functionName", BindingFlags.Public | BindingFlags.Instance);
            string funcName = functionNameProp?.GetValue(node) as string;
            if (string.IsNullOrEmpty(funcName))
            {
                Debug.LogWarning("Rex Tools Sync: Function Name is empty.");
                return;
            }

            // 3. Retrieve function source file reference
            var functionSourceProp = customFunctionNodeType.GetProperty("functionSource", BindingFlags.Public | BindingFlags.Instance);
            string sourceRef = functionSourceProp?.GetValue(node) as string;
            if (string.IsNullOrEmpty(sourceRef))
            {
                Debug.LogWarning("Rex Tools Sync: Source HLSL file is not assigned.");
                return;
            }

            // Resolve path (sourceRef could be path or GUID)
            string assetPath = sourceRef;
            if (sourceRef.Length == 32 && !sourceRef.Contains("/") && !sourceRef.Contains("\\"))
            {
                assetPath = AssetDatabase.GUIDToAssetPath(sourceRef);
            }

            if (string.IsNullOrEmpty(assetPath) || !System.IO.File.Exists(assetPath))
            {
                Debug.LogError($"Rex Tools Sync: Source HLSL file not found at path: '{assetPath}'");
                return;
            }

            // 4. Read file content
            string hlslText;
            try
            {
                hlslText = System.IO.File.ReadAllText(assetPath);
            }
            catch (Exception ex)
            {
                Debug.LogError($"Rex Tools Sync: Failed to read HLSL file at '{assetPath}' - {ex.Message}");
                return;
            }

            // 5. Parse function signature using Regex
            // Match return type, function name (with optional precision suffix), and parameters string
            // Group 1: Return type
            // Group 2: Parameters block
            string escapedFuncName = Regex.Escape(funcName);
            string pattern = $@"\b([a-zA-Z0-9_]+)\s+{escapedFuncName}(?:_float|_half)?\s*\(([^)]*)\)";
            var match = Regex.Match(hlslText, pattern);

            if (!match.Success)
            {
                Debug.LogError($"Rex Tools Sync: Could not find function signature matching '{funcName}' or '{funcName}_float' / '{funcName}_half' in '{assetPath}'.");
                return;
            }

            string returnType = match.Groups[1].Value.Trim();
            string paramsString = match.Groups[2].Value.Trim();

            List<ParsedParameter> parsedParams = ParseParameters(paramsString);
            
            // Output return type as slot if not void
            bool hasReturnValue = returnType != "void";
            
            ApplySlotsToNode(node, parsedParams, hasReturnValue, returnType, funcName, graphView);
        }

        private static List<ParsedParameter> ParseParameters(string paramsString)
        {
            var results = new List<ParsedParameter>();
            if (string.IsNullOrEmpty(paramsString)) return results;

            // Split parameters by top-level commas (respecting nested brackets)
            List<string> paramBlocks = SplitParameters(paramsString);

            foreach (var block in paramBlocks)
            {
                string cleanBlock = block.Trim();
                if (string.IsNullOrEmpty(cleanBlock)) continue;

                // Split words
                string[] tokens = cleanBlock.Split(new[] { ' ', '\t', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
                if (tokens.Length < 2) continue;

                bool isOutput = false;
                string typeStr = "";
                int typeIndex = 0;

                // Look for out/inout qualifiers
                for (int i = 0; i < tokens.Length - 1; i++)
                {
                    string t = tokens[i].ToLower();
                    if (t == "out" || t == "inout")
                    {
                        isOutput = true;
                    }
                    if (t == "out" || t == "in" || t == "inout" || t == "const" || t == "uniform" || t == "inline")
                    {
                        typeIndex = i + 1;
                    }
                }

                if (typeIndex < tokens.Length)
                {
                    typeStr = tokens[typeIndex];
                    string name = tokens[tokens.Length - 1];
                    name = name.TrimEnd(';', ')');

                    if (!string.IsNullOrEmpty(name) && !string.IsNullOrEmpty(typeStr))
                    {
                        results.Add(new ParsedParameter
                        {
                            Name = name,
                            TypeStr = typeStr,
                            IsOutput = isOutput
                        });
                    }
                }
            }

            return results;
        }

        private static List<string> SplitParameters(string paramsString)
        {
            var list = new List<string>();
            int bracketCount = 0;
            int parenCount = 0;
            int start = 0;

            for (int i = 0; i < paramsString.Length; i++)
            {
                char c = paramsString[i];
                if (c == '<') bracketCount++;
                else if (c == '>') bracketCount--;
                else if (c == '(') parenCount++;
                else if (c == ')') parenCount--;
                else if (c == ',' && bracketCount == 0 && parenCount == 0)
                {
                    list.Add(paramsString.Substring(start, i - start));
                    start = i + 1;
                }
            }
            if (start < paramsString.Length)
            {
                list.Add(paramsString.Substring(start));
            }
            return list;
        }

        private static object CreateSlotInstance(string typeStr, int id, string displayName, int slotTypeVal)
        {
            var assembly = Assembly.Load("Unity.ShaderGraph.Editor");
            
            // Clean up precision suffixes from HLSL types if present
            string normType = typeStr.Replace("_float", "").Replace("_half", "").Trim().ToLower();

            // Default stage capability: All
            var stageCapEnum = assembly.GetType("UnityEditor.ShaderGraph.ShaderStageCapability");
            object stageCapAll = Enum.Parse(stageCapEnum, "All");

            // SlotType Enum Value
            object slotTypeEnumValue = Enum.ToObject(slotTypeEnum, slotTypeVal); // 0 = Input, 1 = Output

            switch (normType)
            {
                case "float":
                case "half":
                case "real":
                case "double":
                case "int":
                case "uint":
                case "min16float":
                case "min10float":
                case "min16int":
                    var v1Type = assembly.GetType("UnityEditor.ShaderGraph.Vector1MaterialSlot");
                    return Activator.CreateInstance(v1Type, new object[] {
                        id, displayName, displayName, slotTypeEnumValue, 0.0f, stageCapAll, "X", false
                    });

                case "bool":
                    var boolType = assembly.GetType("UnityEditor.ShaderGraph.BooleanMaterialSlot");
                    return Activator.CreateInstance(boolType, new object[] {
                        id, displayName, displayName, slotTypeEnumValue, false, stageCapAll, false
                    });

                case "float2":
                case "half2":
                case "int2":
                case "uint2":
                    var v2Type = assembly.GetType("UnityEditor.ShaderGraph.Vector2MaterialSlot");
                    return Activator.CreateInstance(v2Type, new object[] {
                        id, displayName, displayName, slotTypeEnumValue, Vector2.zero, stageCapAll, "X", "Y", false, false
                    });

                case "float3":
                case "half3":
                case "int3":
                case "uint3":
                    var v3Type = assembly.GetType("UnityEditor.ShaderGraph.Vector3MaterialSlot");
                    return Activator.CreateInstance(v3Type, new object[] {
                        id, displayName, displayName, slotTypeEnumValue, Vector3.zero, stageCapAll, "X", "Y", "Z", false
                    });

                case "float4":
                case "half4":
                case "int4":
                case "uint4":
                    var v4Type = assembly.GetType("UnityEditor.ShaderGraph.Vector4MaterialSlot");
                    return Activator.CreateInstance(v4Type, new object[] {
                        id, displayName, displayName, slotTypeEnumValue, Vector4.zero, stageCapAll, "X", "Y", "Z", "W", false
                    });

                case "float2x2":
                case "half2x2":
                    var m2Type = assembly.GetType("UnityEditor.ShaderGraph.Matrix2MaterialSlot");
                    return Activator.CreateInstance(m2Type, new object[] {
                        id, displayName, displayName, slotTypeEnumValue, stageCapAll, false
                    });

                case "float3x3":
                case "half3x3":
                    var m3Type = assembly.GetType("UnityEditor.ShaderGraph.Matrix3MaterialSlot");
                    return Activator.CreateInstance(m3Type, new object[] {
                        id, displayName, displayName, slotTypeEnumValue, stageCapAll, false
                    });

                case "float4x4":
                case "half4x4":
                    var m4Type = assembly.GetType("UnityEditor.ShaderGraph.Matrix4MaterialSlot");
                    return Activator.CreateInstance(m4Type, new object[] {
                        id, displayName, displayName, slotTypeEnumValue, stageCapAll, false
                    });

                case "texture2d":
                case "unitytexture2d":
                    var tex2dType = assembly.GetType("UnityEditor.ShaderGraph.Texture2DInputMaterialSlot");
                    return Activator.CreateInstance(tex2dType, new object[] {
                        id, displayName, displayName, stageCapAll, false
                    });

                case "texture3d":
                case "unitytexture3d":
                    var tex3dType = assembly.GetType("UnityEditor.ShaderGraph.Texture3DInputMaterialSlot");
                    return Activator.CreateInstance(tex3dType, new object[] {
                        id, displayName, displayName, stageCapAll, false
                    });

                case "texture2darray":
                case "unitytexture2darray":
                    var texArrayType = assembly.GetType("UnityEditor.ShaderGraph.Texture2DArrayInputMaterialSlot");
                    return Activator.CreateInstance(texArrayType, new object[] {
                        id, displayName, displayName, stageCapAll, false
                    });

                case "samplerstate":
                case "unitysamplerstate":
                    var samplerType = assembly.GetType("UnityEditor.ShaderGraph.SamplerStateMaterialSlot");
                    return Activator.CreateInstance(samplerType, new object[] {
                        id, displayName, displayName, slotTypeEnumValue, stageCapAll, false
                    });

                default:
                    Debug.LogWarning($"Rex Tools Sync: Unrecognized HLSL parameter type '{typeStr}'. Defaulting to float (Vector1).");
                    var defType = assembly.GetType("UnityEditor.ShaderGraph.Vector1MaterialSlot");
                    return Activator.CreateInstance(defType, new object[] {
                        id, displayName, displayName, slotTypeEnumValue, 0.0f, stageCapAll, "X", false
                    });
            }
        }

        private static void ApplySlotsToNode(object node, List<ParsedParameter> parsedParams, bool hasReturnValue, string returnType, string funcName, GraphView graphView)
        {
            var assembly = Assembly.Load("Unity.ShaderGraph.Editor");
            var nodeBaseType = assembly.GetType("UnityEditor.ShaderGraph.AbstractMaterialNode");

            // 1. Get existing slots
            var getSlotsMethod = nodeBaseType.GetMethod("GetSlots", BindingFlags.Public | BindingFlags.Instance, null, new[] { typeof(List<object>) }, null);
            if (getSlotsMethod == null)
            {
                getSlotsMethod = nodeBaseType.GetMethod("GetSlots", BindingFlags.Public | BindingFlags.Instance);
            }

            var currentSlotsList = new List<object>();
            
            // Direct field access as fallback
            var slotsField = nodeBaseType.GetField("m_Slots", BindingFlags.NonPublic | BindingFlags.Instance);
            if (slotsField != null)
            {
                var rawSlotsList = slotsField.GetValue(node) as System.Collections.IEnumerable;
                if (rawSlotsList != null)
                {
                    foreach (var rawSlotWrapper in rawSlotsList)
                    {
                        var valueProp = rawSlotWrapper.GetType().GetProperty("value", BindingFlags.Public | BindingFlags.Instance);
                        var slotObj = valueProp?.GetValue(rawSlotWrapper);
                        if (slotObj != null)
                        {
                            currentSlotsList.Add(slotObj);
                        }
                    }
                }
            }
            else if (getSlotsMethod != null)
            {
                getSlotsMethod.Invoke(node, new object[] { currentSlotsList });
            }

            // Create dictionary of existing slots indexed by lowercase displayName
            var existingSlotsByName = new Dictionary<string, object>();
            var idsToKeep = new HashSet<int>();
            int maxSlotId = 0;

            var slotBaseType = assembly.GetType("UnityEditor.ShaderGraph.MaterialSlot");
            var idProp = slotBaseType.GetProperty("id", BindingFlags.Public | BindingFlags.Instance);
            var displayNameProp = slotBaseType.GetProperty("displayName", BindingFlags.Public | BindingFlags.Instance);

            foreach (var slot in currentSlotsList)
            {
                int id = (int)idProp.GetValue(slot);
                string name = displayNameProp.GetValue(slot) as string;
                if (!string.IsNullOrEmpty(name))
                {
                    existingSlotsByName[name.ToLower()] = slot;
                }
                maxSlotId = Math.Max(maxSlotId, id);
            }

            // 2. Build new list of slots, preserving matching ones
            var newSlots = new List<object>();
            int nextId = maxSlotId + 1;

            // Handle return value output if any (convention name: "Out")
            if (hasReturnValue)
            {
                string retName = "Out";
                object existingSlot = null;
                bool match = false;

                if (existingSlotsByName.TryGetValue(retName.ToLower(), out existingSlot))
                {
                    string normExistingType = existingSlot.GetType().Name.Replace("MaterialSlot", "").ToLower();
                    string normNewType = returnType.Replace("_float", "").Replace("_half", "").Trim().ToLower();

                    if (normNewType == "float" || normNewType == "half" || normNewType == "real") normNewType = "vector1";
                    else if (normNewType == "float2" || normNewType == "half2") normNewType = "vector2";
                    else if (normNewType == "float3" || normNewType == "half3") normNewType = "vector3";
                    else if (normNewType == "float4" || normNewType == "half4") normNewType = "vector4";

                    if (normExistingType == normNewType)
                    {
                        match = true;
                        int id = (int)idProp.GetValue(existingSlot);
                        idsToKeep.Add(id);
                        newSlots.Add(existingSlot);
                    }
                }

                if (!match)
                {
                    int id = nextId++;
                    object newSlot = CreateSlotInstance(returnType, id, retName, 1); // 1 = Output
                    if (newSlot != null)
                    {
                        newSlots.Add(newSlot);
                    }
                }
            }

            // Handle function parameters
            foreach (var param in parsedParams)
            {
                object existingSlot = null;
                bool match = false;

                if (existingSlotsByName.TryGetValue(param.Name.ToLower(), out existingSlot))
                {
                    string normExistingType = existingSlot.GetType().Name.Replace("MaterialSlot", "").ToLower();
                    string normNewType = param.TypeStr.Replace("_float", "").Replace("_half", "").Trim().ToLower();

                    if (normNewType == "float" || normNewType == "half" || normNewType == "real") normNewType = "vector1";
                    else if (normNewType == "float2" || normNewType == "half2") normNewType = "vector2";
                    else if (normNewType == "float3" || normNewType == "half3") normNewType = "vector3";
                    else if (normNewType == "float4" || normNewType == "half4") normNewType = "vector4";

                    if (normExistingType == normNewType)
                    {
                        match = true;
                        int id = (int)idProp.GetValue(existingSlot);
                        idsToKeep.Add(id);
                        newSlots.Add(existingSlot);
                    }
                }

                if (!match)
                {
                    int id = nextId++;
                    object newSlot = CreateSlotInstance(param.TypeStr, id, param.Name, param.IsOutput ? 1 : 0);
                    if (newSlot != null)
                    {
                        newSlots.Add(newSlot);
                    }
                }
            }

            // 3. Register Undo on the GraphObject owner
            var ownerProp = nodeBaseType.GetProperty("owner", BindingFlags.Public | BindingFlags.Instance);
            var graphData = ownerProp?.GetValue(node);
            if (graphData != null)
            {
                var graphDataOwnerProp = graphData.GetType().GetProperty("owner", BindingFlags.Public | BindingFlags.Instance);
                if (graphDataOwnerProp != null)
                {
                    var graphObject = graphDataOwnerProp.GetValue(graphData) as UnityEngine.Object;
                    if (graphObject != null)
                    {
                        Undo.RegisterCompleteObjectUndo(graphObject, "Sync Custom Function Ports");
                    }
                }
            }

            // 4. Remove old slots not kept
            var removeSlotMethod = nodeBaseType.GetMethod("RemoveSlot", BindingFlags.Public | BindingFlags.Instance);
            if (removeSlotMethod != null)
            {
                foreach (var slot in currentSlotsList)
                {
                    int id = (int)idProp.GetValue(slot);
                    if (!idsToKeep.Contains(id))
                    {
                        removeSlotMethod.Invoke(node, new object[] { id });
                    }
                }
            }

            // 5. Add new slots
            var addSlotMethod = nodeBaseType.GetMethod("AddSlot", BindingFlags.Public | BindingFlags.Instance);
            if (addSlotMethod != null)
            {
                foreach (var newSlot in newSlots)
                {
                    int id = (int)idProp.GetValue(newSlot);
                    if (!idsToKeep.Contains(id))
                    {
                        addSlotMethod.Invoke(node, new object[] { newSlot, false });
                    }
                }
            }

            // 6. Notify and Refresh
            var onSlotsChangedMethod = nodeBaseType.GetMethod("OnSlotsChanged", BindingFlags.Public | BindingFlags.Instance);
            onSlotsChangedMethod?.Invoke(node, null);

            var dirtyMethod = nodeBaseType.GetMethod("Dirty", BindingFlags.Public | BindingFlags.Instance);
            if (dirtyMethod != null && modificationScopeEnum != null)
            {
                object graphScope = Enum.Parse(modificationScopeEnum, "Graph");
                dirtyMethod.Invoke(node, new[] { graphScope });
            }

            Debug.Log($"Rex Tools Sync: Successfully synchronized ports for custom function node '{funcName}'. Preserved {idsToKeep.Count} connected ports.");
            
            // Mark the visual graph dirty to repaint connections and ports
            graphView.MarkDirtyRepaint();
        }
    }
}
