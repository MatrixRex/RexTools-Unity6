using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

namespace RexTools.ShaderGraphOutputPreview.Editor
{
    /// <summary>
    /// Adds a context menu item to Unity ShaderGraph when a node is selected,
    /// enabling the user to preview any of its output slots by connecting it to
    /// the Base Color (Unlit) or Emission (Lit) fragment outputs.
    /// Supports restoring the previous connection afterwards.
    /// </summary>
    [InitializeOnLoad]
    public static class ShaderGraphOutputPreviewExtension
    {
        private static Type materialGraphEditWindowType;
        private static Type graphEditorViewType;
        private static Type materialGraphViewType;
        private static Type materialNodeViewType;
        private static Type abstractMaterialNodeType;
        private static Type blockNodeType;
        private static Type materialSlotType;
        private static Type slotReferenceType;
        private static Type modificationScopeEnum;

        // Tracks graph views that already have the contextual menu callback registered
        private static readonly HashSet<int> attachedGraphViews = new HashSet<int>();

        private struct SavedConnection
        {
            public Guid NodeId;
            public int SlotId;
        }

        // Stores the original connection before a preview was made (keyed by GraphView hash code)
        private static readonly Dictionary<int, SavedConnection> savedConnections = new Dictionary<int, SavedConnection>();

        static ShaderGraphOutputPreviewExtension()
        {
            try
            {
                var assembly = Assembly.Load("Unity.ShaderGraph.Editor");
                materialGraphEditWindowType = assembly.GetType("UnityEditor.ShaderGraph.Drawing.MaterialGraphEditWindow");
                graphEditorViewType = assembly.GetType("UnityEditor.ShaderGraph.Drawing.GraphEditorView");
                materialGraphViewType = assembly.GetType("UnityEditor.ShaderGraph.Drawing.MaterialGraphView");
                materialNodeViewType = assembly.GetType("UnityEditor.ShaderGraph.Drawing.MaterialNodeView");
                abstractMaterialNodeType = assembly.GetType("UnityEditor.ShaderGraph.AbstractMaterialNode");
                blockNodeType = assembly.GetType("UnityEditor.ShaderGraph.BlockNode");
                materialSlotType = assembly.GetType("UnityEditor.ShaderGraph.MaterialSlot");
                slotReferenceType = assembly.GetType("UnityEditor.Graphing.SlotReference");
                modificationScopeEnum = assembly.GetType("UnityEditor.Graphing.ModificationScope");

                EditorApplication.update += OnEditorUpdate;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"ShaderGraph Output Preview: Failed to initialize reflection - {e.Message}");
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
            int graphId = graphView.GetHashCode();
            bool hasSavedConnection = savedConnections.TryGetValue(graphId, out var saved);

            object selectedNode = null;
            var outputSlots = new List<(string name, object slotRef)>();

            // Get selected node if only one is selected
            if (graphView.selection != null && graphView.selection.Count == 1)
            {
                var selected = graphView.selection[0];
                if (materialNodeViewType != null && materialNodeViewType.IsInstanceOfType(selected))
                {
                    var nodeProperty = materialNodeViewType.GetProperty("node", BindingFlags.Public | BindingFlags.Instance);
                    if (nodeProperty != null)
                    {
                        selectedNode = nodeProperty.GetValue(selected);
                        if (selectedNode != null && abstractMaterialNodeType != null && materialSlotType != null)
                        {
                            var slots = GetSlotsSafe(selectedNode);
                            if (slots != null)
                            {
                                var isOutputSlotProp = materialSlotType.GetProperty("isOutputSlot", BindingFlags.Public | BindingFlags.Instance);
                                var displayNameProp = materialSlotType.GetProperty("displayName", BindingFlags.Public | BindingFlags.Instance);
                                var slotReferenceProp = materialSlotType.GetProperty("slotReference", BindingFlags.Public | BindingFlags.Instance);

                                foreach (var slot in slots)
                                {
                                    if (isOutputSlotProp != null && (bool)isOutputSlotProp.GetValue(slot))
                                    {
                                        string name = displayNameProp?.GetValue(slot) as string ?? "Output";
                                        object slotRef = slotReferenceProp?.GetValue(slot);
                                        if (slotRef != null)
                                        {
                                            outputSlots.Add((name, slotRef));
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }

            if (outputSlots.Count > 0 || hasSavedConnection)
            {
                evt.menu.AppendSeparator();

                foreach (var slotInfo in outputSlots)
                {
                    evt.menu.AppendAction($"Preview Output/{slotInfo.name}", action =>
                    {
                        ConnectToPreview(graphView, selectedNode, slotInfo.slotRef);
                    });
                }

                if (hasSavedConnection)
                {
                    string actionLabel = (saved.NodeId == Guid.Empty) ? "Preview Output/Clear Preview Connection" : "Preview Output/Restore Connection";
                    evt.menu.AppendAction(actionLabel, action =>
                    {
                        RestoreConnection(graphView, selectedNode);
                    });
                }
            }
        }

        private static IEnumerable<object> GetNodesSafe(object graphData)
        {
            if (graphData == null) return Enumerable.Empty<object>();

            var graphDataType = graphData.GetType();

            // Try "nodes" property
            var nodesProp = graphDataType.GetProperty("nodes", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (nodesProp != null)
            {
                var nodesVal = nodesProp.GetValue(graphData) as IEnumerable;
                if (nodesVal != null) return nodesVal.Cast<object>();
            }

            // Try generic "GetNodes" method
            var getNodesMethod = graphDataType.GetMethod("GetNodes", BindingFlags.Public | BindingFlags.Instance);
            if (getNodesMethod != null)
            {
                try
                {
                    var genericGetNodes = getNodesMethod.MakeGenericMethod(abstractMaterialNodeType);
                    var nodesVal = genericGetNodes.Invoke(graphData, null) as IEnumerable;
                    if (nodesVal != null) return nodesVal.Cast<object>();
                }
                catch { }
            }

            // Try "m_Nodes" field
            var nodesField = graphDataType.GetField("m_Nodes", BindingFlags.NonPublic | BindingFlags.Instance);
            if (nodesField != null)
            {
                var nodesVal = nodesField.GetValue(graphData) as IEnumerable;
                if (nodesVal != null) return nodesVal.Cast<object>();
            }

            return Enumerable.Empty<object>();
        }

        private static void ConnectToPreview(GraphView graphView, object selectedNode, object outputSlotRef)
        {
            if (abstractMaterialNodeType == null || slotReferenceType == null || selectedNode == null) return;

            // Retrieve the GraphData owner
            var ownerProp = abstractMaterialNodeType.GetProperty("owner", BindingFlags.Public | BindingFlags.Instance);
            var graphData = ownerProp?.GetValue(selectedNode);
            if (graphData == null)
            {
                Debug.LogWarning("Rex Tools Preview Output: Could not find GraphData owner for the selected node.");
                return;
            }

            var graphDataType = graphData.GetType();

            // Find target block nodes (Emission for Lit, Base Color for Unlit)
            var nodes = GetNodesSafe(graphData);
            
            object emissionBlock = null;
            object baseColorBlock = null;

            var descriptorProp = blockNodeType?.GetProperty("descriptor", BindingFlags.Public | BindingFlags.Instance);

            int totalNodesCount = 0;
            int blockNodesCount = 0;

            foreach (var node in nodes)
            {
                totalNodesCount++;
                if (blockNodeType != null && blockNodeType.IsInstanceOfType(node))
                {
                    blockNodesCount++;
                    
                    // Direct displayName check on BlockNode (inherits from AbstractMaterialNode)
                    var nodeDisplayNameProp = abstractMaterialNodeType.GetProperty("displayName", BindingFlags.Public | BindingFlags.Instance);
                    string nodeDisplayName = nodeDisplayNameProp?.GetValue(node) as string;

                    var descriptor = descriptorProp?.GetValue(node);
                    string descriptorDisplayName = null;
                    string descriptorName = null;
                    string descriptorPath = null;

                    if (descriptor != null)
                    {
                        var dispNameProp = descriptor.GetType().GetProperty("displayName", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                        descriptorDisplayName = dispNameProp?.GetValue(descriptor) as string;

                        var nameProp = descriptor.GetType().GetProperty("name", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                        descriptorName = nameProp?.GetValue(descriptor) as string;

                        var pathProp = descriptor.GetType().GetProperty("path", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                        descriptorPath = pathProp?.GetValue(descriptor) as string;
                    }

                    // Check all possible identifiers for Emission or Base Color
                    bool isEmission = false;
                    bool isBaseColor = false;

                    string[] sourceStrings = { nodeDisplayName, descriptorDisplayName, descriptorName, descriptorPath };
                    foreach (var s in sourceStrings)
                    {
                        if (string.IsNullOrEmpty(s)) continue;
                        if (s.IndexOf("Emission", StringComparison.OrdinalIgnoreCase) >= 0) isEmission = true;
                        if (s.IndexOf("Base Color", StringComparison.OrdinalIgnoreCase) >= 0 || 
                            s.IndexOf("BaseColor", StringComparison.OrdinalIgnoreCase) >= 0 ||
                            s.IndexOf("Albedo", StringComparison.OrdinalIgnoreCase) >= 0) isBaseColor = true;
                    }

                    if (isEmission)
                    {
                        emissionBlock = node;
                    }
                    else if (isBaseColor)
                    {
                        baseColorBlock = node;
                    }
                }
            }

            Debug.Log($"[Preview Output] Scanned {totalNodesCount} nodes ({blockNodesCount} blocks). Found Emission={emissionBlock != null}, BaseColor={baseColorBlock != null}");

            // lit shader uses emission, unlit uses basecolor
            object targetBlock = emissionBlock ?? baseColorBlock;
            if (targetBlock == null)
            {
                Debug.LogWarning("Rex Tools Preview Output: Could not find Emission or Base Color output block node in the graph.");
                return;
            }

            object targetInputSlotRef = GetBlockNodeInputSlotRef(targetBlock);
            if (targetInputSlotRef == null)
            {
                Debug.LogWarning("Rex Tools Preview Output: Could not find input slot on the target preview block node.");
                return;
            }

            // Save previous connection (even if null, so we know we can restore to empty)
            int graphId = graphView.GetHashCode();
            object prevSourceSlotRef = GetConnectedSourceSlotRef(graphData, targetInputSlotRef);
            if (prevSourceSlotRef != null)
            {
                var nodeIdProp = slotReferenceType.GetProperty("nodeId", BindingFlags.Public | BindingFlags.Instance);
                var slotIdProp = slotReferenceType.GetProperty("slotId", BindingFlags.Public | BindingFlags.Instance);
                if (nodeIdProp != null && slotIdProp != null)
                {
                    Guid nodeId = (Guid)nodeIdProp.GetValue(prevSourceSlotRef);
                    int slotId = (int)slotIdProp.GetValue(prevSourceSlotRef);

                    // Verify it's not the same connection we are about to make
                    var outNodeIdProp = slotReferenceType.GetProperty("nodeId", BindingFlags.Public | BindingFlags.Instance);
                    var outSlotIdProp = slotReferenceType.GetProperty("slotId", BindingFlags.Public | BindingFlags.Instance);
                    Guid outNodeId = (Guid)outNodeIdProp.GetValue(outputSlotRef);
                    int outSlotId = (int)outSlotIdProp.GetValue(outputSlotRef);

                    if (nodeId != outNodeId || slotId != outSlotId)
                    {
                        savedConnections[graphId] = new SavedConnection { NodeId = nodeId, SlotId = slotId };
                        Debug.Log("[Preview Output] Saved original connection to restore later.");
                    }
                }
            }
            else
            {
                // Save empty connection so we can restore to empty
                savedConnections[graphId] = new SavedConnection { NodeId = Guid.Empty, SlotId = -1 };
                Debug.Log("[Preview Output] Saved empty connection to restore later.");
            }

            // Register complete undo on the graph object owner
            var graphDataOwnerProp = graphDataType.GetProperty("owner", BindingFlags.Public | BindingFlags.Instance);
            if (graphDataOwnerProp != null)
            {
                var graphObject = graphDataOwnerProp.GetValue(graphData) as UnityEngine.Object;
                if (graphObject != null)
                {
                    Undo.RegisterCompleteObjectUndo(graphObject, "Preview Output Slot");
                }
            }

            // Remove any existing connections to the target input slot
            RemoveConnectionsToSlot(graphData, targetInputSlotRef);

            // Connect output to target
            var connectMethod = graphDataType.GetMethod("Connect", new[] { slotReferenceType, slotReferenceType });
            if (connectMethod == null)
            {
                connectMethod = graphDataType.GetMethods(BindingFlags.Public | BindingFlags.Instance)
                    .FirstOrDefault(m => m.Name == "Connect" && m.GetParameters().Length == 2);
            }

            if (connectMethod != null)
            {
                connectMethod.Invoke(graphData, new[] { outputSlotRef, targetInputSlotRef });
                Debug.Log($"[Preview Output] Successfully connected to {(emissionBlock != null ? "Emission" : "Base Color")} block!");

                // Dirty the graph to compile and validate changes
                var dirtyMethod = abstractMaterialNodeType.GetMethod("Dirty", BindingFlags.Public | BindingFlags.Instance);
                if (dirtyMethod != null && modificationScopeEnum != null)
                {
                    object graphScope = Enum.Parse(modificationScopeEnum, "Graph");
                    dirtyMethod.Invoke(selectedNode, new[] { graphScope });
                }

                // Repaint visual representation
                graphView.MarkDirtyRepaint();
            }
            else
            {
                Debug.LogWarning("Rex Tools Preview Output: Connect method not found on GraphData.");
            }
        }

        private static void RestoreConnection(GraphView graphView, object selectedNode)
        {
            if (abstractMaterialNodeType == null || slotReferenceType == null) return;

            int graphId = graphView.GetHashCode();
            if (!savedConnections.TryGetValue(graphId, out var saved))
            {
                Debug.LogWarning("Rex Tools Preview Output: No saved connection found to restore.");
                return;
            }

            // If selectedNode is null (right-clicked empty space), try to find any node view in the graph view to locate GraphData owner
            if (selectedNode == null)
            {
                if (materialNodeViewType != null)
                {
                    var firstNodeView = graphView.Query<VisualElement>().ToList().FirstOrDefault(element => materialNodeViewType.IsInstanceOfType(element));
                    if (firstNodeView != null)
                    {
                        var nodeProperty = materialNodeViewType.GetProperty("node", BindingFlags.Public | BindingFlags.Instance);
                        selectedNode = nodeProperty?.GetValue(firstNodeView);
                    }
                }
            }

            if (selectedNode == null)
            {
                Debug.LogWarning("Rex Tools Preview Output: Could not find any node in the graph to retrieve GraphData owner.");
                return;
            }

            // Retrieve the GraphData owner
            var ownerProp = abstractMaterialNodeType.GetProperty("owner", BindingFlags.Public | BindingFlags.Instance);
            var graphData = ownerProp?.GetValue(selectedNode);
            if (graphData == null) return;

            var graphDataType = graphData.GetType();

            // Find target block nodes
            var nodes = GetNodesSafe(graphData);
            object emissionBlock = null;
            object baseColorBlock = null;
            var descriptorProp = blockNodeType?.GetProperty("descriptor", BindingFlags.Public | BindingFlags.Instance);

            foreach (var node in nodes)
            {
                if (blockNodeType != null && blockNodeType.IsInstanceOfType(node))
                {
                    var descriptor = descriptorProp?.GetValue(node);
                    if (descriptor != null)
                    {
                        var displayNameProp = descriptor.GetType().GetProperty("displayName", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                        string displayName = displayNameProp?.GetValue(descriptor) as string;

                        if (string.Equals(displayName, "Emission", StringComparison.OrdinalIgnoreCase))
                        {
                            emissionBlock = node;
                        }
                        else if (string.Equals(displayName, "Base Color", StringComparison.OrdinalIgnoreCase) ||
                                 string.Equals(displayName, "BaseColor", StringComparison.OrdinalIgnoreCase))
                        {
                            baseColorBlock = node;
                        }
                    }
                }
            }

            object targetBlock = emissionBlock ?? baseColorBlock;
            if (targetBlock == null) return;

            object targetInputSlotRef = GetBlockNodeInputSlotRef(targetBlock);
            if (targetInputSlotRef == null) return;

            if (saved.NodeId == Guid.Empty && saved.SlotId == -1)
            {
                // Register undo
                var graphDataOwnerProp = graphDataType.GetProperty("owner", BindingFlags.Public | BindingFlags.Instance);
                if (graphDataOwnerProp != null)
                {
                    var graphObject = graphDataOwnerProp.GetValue(graphData) as UnityEngine.Object;
                    if (graphObject != null)
                    {
                        Undo.RegisterCompleteObjectUndo(graphObject, "Disconnect Preview Connection");
                    }
                }

                // Just remove current preview connections
                RemoveConnectionsToSlot(graphData, targetInputSlotRef);
                Debug.Log("[Preview Output] Disconnected preview connection successfully!");

                savedConnections.Remove(graphId);

                // Dirty the graph
                var dirtyMethod = abstractMaterialNodeType.GetMethod("Dirty", BindingFlags.Public | BindingFlags.Instance);
                if (dirtyMethod != null && modificationScopeEnum != null)
                {
                    object graphScope = Enum.Parse(modificationScopeEnum, "Graph");
                    dirtyMethod.Invoke(selectedNode, new[] { graphScope });
                }

                graphView.MarkDirtyRepaint();
                return;
            }

            // Reconstruct saved output SlotReference
            object outputSlotRef = Activator.CreateInstance(slotReferenceType, saved.NodeId, saved.SlotId);

            // Register undo
            var graphDataOwnerProp2 = graphDataType.GetProperty("owner", BindingFlags.Public | BindingFlags.Instance);
            if (graphDataOwnerProp2 != null)
            {
                var graphObject = graphDataOwnerProp2.GetValue(graphData) as UnityEngine.Object;
                if (graphObject != null)
                {
                    Undo.RegisterCompleteObjectUndo(graphObject, "Restore Preview Connection");
                }
            }

            // Remove current preview connections
            RemoveConnectionsToSlot(graphData, targetInputSlotRef);

            // Connect restored output to target
            var connectMethod = graphDataType.GetMethod("Connect", new[] { slotReferenceType, slotReferenceType });
            if (connectMethod == null)
            {
                connectMethod = graphDataType.GetMethods(BindingFlags.Public | BindingFlags.Instance)
                    .FirstOrDefault(m => m.Name == "Connect" && m.GetParameters().Length == 2);
            }

            if (connectMethod != null)
            {
                connectMethod.Invoke(graphData, new[] { outputSlotRef, targetInputSlotRef });
                Debug.Log("[Preview Output] Restored previous connection successfully!");

                // Remove from dictionary
                savedConnections.Remove(graphId);

                // Dirty the graph
                var dirtyMethod = abstractMaterialNodeType.GetMethod("Dirty", BindingFlags.Public | BindingFlags.Instance);
                if (dirtyMethod != null && modificationScopeEnum != null)
                {
                    object graphScope = Enum.Parse(modificationScopeEnum, "Graph");
                    dirtyMethod.Invoke(selectedNode, new[] { graphScope });
                }

                // Repaint
                graphView.MarkDirtyRepaint();
            }
        }

        private static IEnumerable GetSlotsSafe(object node)
        {
            if (abstractMaterialNodeType == null || node == null) return null;

            // Direct field access as fallback (very reliable)
            var slotsField = abstractMaterialNodeType.GetField("m_Slots", BindingFlags.NonPublic | BindingFlags.Instance);
            if (slotsField != null)
            {
                var rawSlotsList = slotsField.GetValue(node) as IEnumerable;
                if (rawSlotsList != null)
                {
                    var resultList = new List<object>();
                    foreach (var rawSlotWrapper in rawSlotsList)
                    {
                        var valueProp = rawSlotWrapper.GetType().GetProperty("value", BindingFlags.Public | BindingFlags.Instance);
                        var slotObj = valueProp?.GetValue(rawSlotWrapper);
                        if (slotObj != null)
                        {
                            resultList.Add(slotObj);
                        }
                    }
                    return resultList;
                }
            }

            var getSlotsMethod = abstractMaterialNodeType.GetMethod("GetSlots", BindingFlags.Public | BindingFlags.Instance);
            if (getSlotsMethod != null)
            {
                var parameters = getSlotsMethod.GetParameters();
                if (parameters.Length == 1)
                {
                    var listType = parameters[0].ParameterType;
                    var listInstance = Activator.CreateInstance(listType);
                    getSlotsMethod.Invoke(node, new object[] { listInstance });
                    return listInstance as IEnumerable;
                }
            }

            return null;
        }

        private static object GetBlockNodeInputSlotRef(object blockNode)
        {
            if (abstractMaterialNodeType == null || materialSlotType == null) return null;

            var slots = GetSlotsSafe(blockNode);
            if (slots == null) return null;

            var isInputSlotProp = materialSlotType.GetProperty("isInputSlot", BindingFlags.Public | BindingFlags.Instance);
            var slotReferenceProp = materialSlotType.GetProperty("slotReference", BindingFlags.Public | BindingFlags.Instance);

            foreach (var slot in slots)
            {
                if (isInputSlotProp != null && (bool)isInputSlotProp.GetValue(slot))
                {
                    return slotReferenceProp?.GetValue(slot);
                }
            }
            return null;
        }

        private static object GetConnectedSourceSlotRef(object graphData, object slotRef)
        {
            var graphDataType = graphData.GetType();

            // Try to find IEnumerable<IEdge> GetEdges(SlotReference)
            var getEdgesMethod = graphDataType.GetMethod("GetEdges", new[] { slotReferenceType });
            IEnumerable edges = null;

            if (getEdgesMethod != null && getEdgesMethod.ReturnType != typeof(void))
            {
                edges = getEdgesMethod.Invoke(graphData, new[] { slotRef }) as IEnumerable;
            }
            else
            {
                // Try void GetEdges(SlotReference, List<IEdge>)
                var getEdgesListMethod = graphDataType.GetMethods(BindingFlags.Public | BindingFlags.Instance)
                    .FirstOrDefault(m => m.Name == "GetEdges" && m.GetParameters().Length == 2);
                if (getEdgesListMethod != null)
                {
                    var iedgeType = slotReferenceType.Assembly.GetType("UnityEditor.Graphing.IEdge") ?? 
                                    slotReferenceType.Assembly.GetType("UnityEditor.ShaderGraph.IEdge");
                    if (iedgeType != null)
                    {
                        var listType = typeof(List<>).MakeGenericType(iedgeType);
                        var listInstance = Activator.CreateInstance(listType);
                        getEdgesListMethod.Invoke(graphData, new[] { slotRef, listInstance });
                        edges = listInstance as IEnumerable;
                    }
                }
            }

            if (edges != null)
            {
                foreach (var edge in edges)
                {
                    var outputSlotProp = edge.GetType().GetProperty("outputSlot", BindingFlags.Public | BindingFlags.Instance);
                    if (outputSlotProp != null)
                    {
                        return outputSlotProp.GetValue(edge);
                    }
                }
            }

            return null;
        }

        private static void RemoveConnectionsToSlot(object graphData, object slotRef)
        {
            var graphDataType = graphData.GetType();

            // Try to find IEnumerable<IEdge> GetEdges(SlotReference)
            var getEdgesMethod = graphDataType.GetMethod("GetEdges", new[] { slotReferenceType });
            IEnumerable edges = null;

            if (getEdgesMethod != null && getEdgesMethod.ReturnType != typeof(void))
            {
                edges = getEdgesMethod.Invoke(graphData, new[] { slotRef }) as IEnumerable;
            }
            else
            {
                // Try void GetEdges(SlotReference, List<IEdge>)
                var getEdgesListMethod = graphDataType.GetMethods(BindingFlags.Public | BindingFlags.Instance)
                    .FirstOrDefault(m => m.Name == "GetEdges" && m.GetParameters().Length == 2);
                if (getEdgesListMethod != null)
                {
                    var iedgeType = slotReferenceType.Assembly.GetType("UnityEditor.Graphing.IEdge") ?? 
                                    slotReferenceType.Assembly.GetType("UnityEditor.ShaderGraph.IEdge");
                    if (iedgeType != null)
                    {
                        var listType = typeof(List<>).MakeGenericType(iedgeType);
                        var listInstance = Activator.CreateInstance(listType);
                        getEdgesListMethod.Invoke(graphData, new[] { slotRef, listInstance });
                        edges = listInstance as IEnumerable;
                    }
                }
            }

            if (edges != null)
            {
                var removeEdgeMethod = graphDataType.GetMethod("RemoveEdge", BindingFlags.Public | BindingFlags.Instance);
                if (removeEdgeMethod == null)
                {
                    removeEdgeMethod = graphDataType.GetMethods(BindingFlags.Public | BindingFlags.Instance)
                        .FirstOrDefault(m => m.Name == "RemoveEdge" && m.GetParameters().Length == 1);
                }

                if (removeEdgeMethod != null)
                {
                    var edgesList = new List<object>();
                    foreach (var edge in edges)
                    {
                        edgesList.Add(edge);
                    }
                    foreach (var edge in edgesList)
                    {
                        removeEdgeMethod.Invoke(graphData, new[] { edge });
                    }
                }
            }
        }
    }
}
