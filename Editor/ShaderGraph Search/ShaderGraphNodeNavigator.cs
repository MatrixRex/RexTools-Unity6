using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Rextools.ShaderGraphSearch.Editor
{
    /// <summary>
    /// Handles connection-based node navigation in the ShaderGraph editor.
    /// Allows navigating forward/backward through connected nodes, with branch
    /// memory for nodes that have multiple output connections.
    /// </summary>
    public class ShaderGraphNodeNavigator
    {
        // Reflection types
        private readonly Type materialGraphViewType;
        private readonly Type materialNodeViewType;
        private readonly Type propertyNodeViewType;
        private readonly Type graphEditorViewType;

        // UI references
        private readonly Button backButton;
        private readonly Button forwardButton;
        private readonly Button upButton;
        private readonly Button downButton;
        private readonly Label branchLabel;

        // Graph references
        private VisualElement graphView;
        private VisualElement graphEditorView;

        // Navigation state
        private readonly Stack<VisualElement> navigationHistory = new Stack<VisualElement>();
        private List<VisualElement> currentBranches = new List<VisualElement>();
        private int currentBranchIndex = 0;
        private VisualElement currentNode = null;

        // Track the last known selected node from the graph to detect user-initiated selections
        private VisualElement lastKnownSelectedNode = null;

        public ShaderGraphNodeNavigator(
            Type materialGraphViewType,
            Type materialNodeViewType,
            Type propertyNodeViewType,
            Type graphEditorViewType,
            Button backButton,
            Button forwardButton,
            Button upButton,
            Button downButton,
            Label branchLabel)
        {
            this.materialGraphViewType = materialGraphViewType;
            this.materialNodeViewType = materialNodeViewType;
            this.propertyNodeViewType = propertyNodeViewType;
            this.graphEditorViewType = graphEditorViewType;
            this.backButton = backButton;
            this.forwardButton = forwardButton;
            this.upButton = upButton;
            this.downButton = downButton;
            this.branchLabel = branchLabel;

            // Wire up button events
            backButton.clicked += NavigateBack;
            forwardButton.clicked += NavigateForward;
            upButton.clicked += NavigateUp;
            downButton.clicked += NavigateDown;

            UpdateUI();
        }

        public void SetGraphView(VisualElement graphView)
        {
            this.graphView = graphView;
        }

        public void SetGraphEditorView(VisualElement graphEditorView)
        {
            this.graphEditorView = graphEditorView;
        }

        /// <summary>
        /// Attempts to resolve the graphView reference from graphEditorView if not yet set.
        /// </summary>
        private bool TryResolveGraphView()
        {
            if (graphView != null) return true;
            if (graphEditorView == null || graphEditorViewType == null)
            {
                Debug.Log($"[ShaderGraph Navigator] TryResolve: graphEditorView={graphEditorView != null}, graphEditorViewType={graphEditorViewType != null}");
                return false;
            }

            try
            {
                // Try property access first
                var gvProp = graphEditorViewType.GetProperty("graphView",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (gvProp != null)
                {
                    var gv = gvProp.GetValue(graphEditorView) as VisualElement;
                    if (gv != null)
                    {
                        Debug.Log($"[ShaderGraph Navigator] Resolved graphView via property: {gv.GetType().Name}");
                        graphView = gv;
                        return true;
                    }
                }

                // Try field access
                var gvField = graphEditorViewType.GetField("m_GraphView",
                    BindingFlags.NonPublic | BindingFlags.Instance);
                if (gvField == null)
                {
                    gvField = graphEditorViewType.GetField("m_graphView",
                        BindingFlags.NonPublic | BindingFlags.Instance);
                }
                if (gvField != null)
                {
                    var gv = gvField.GetValue(graphEditorView) as VisualElement;
                    if (gv != null)
                    {
                        Debug.Log($"[ShaderGraph Navigator] Resolved graphView via field: {gv.GetType().Name}");
                        graphView = gv;
                        return true;
                    }
                }

                // Fallback: search the visual tree for MaterialGraphView
                VisualElement found = null;
                graphEditorView.Query<VisualElement>().ForEach(element =>
                {
                    if (found == null && materialGraphViewType != null && materialGraphViewType.IsInstanceOfType(element))
                    {
                        found = element;
                    }
                });
                if (found != null)
                {
                    Debug.Log($"[ShaderGraph Navigator] Resolved graphView via visual tree search: {found.GetType().Name}");
                    graphView = found;
                    return true;
                }

                // Log available members for debugging
                Debug.Log($"[ShaderGraph Navigator] Could not resolve graphView. GraphEditorView type: {graphEditorView.GetType().FullName}");
                var props = graphEditorViewType.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                foreach (var p in props)
                {
                    if (p.Name.ToLower().Contains("graph"))
                        Debug.Log($"  Property: {p.Name} (Type: {p.PropertyType.Name})");
                }
                var fields = graphEditorViewType.GetFields(BindingFlags.NonPublic | BindingFlags.Instance);
                foreach (var f in fields)
                {
                    if (f.Name.ToLower().Contains("graph"))
                        Debug.Log($"  Field: {f.Name} (Type: {f.FieldType.Name})");
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[ShaderGraph Navigator] TryResolve error: {e.Message}");
            }

            return false;
        }

        /// <summary>
        /// Called periodically to check if the user selected a different node manually.
        /// If so, resets navigation state to treat the new selection as the current node.
        /// </summary>
        public void CheckSelectionChanged()
        {
            // Try to resolve graphView if not yet available
            if (graphView == null)
            {
                if (!TryResolveGraphView()) return;
            }

            var selectedNode = GetSelectedNodeView();
            if (selectedNode == null) return;

            // If the selected node changed and it wasn't us who changed it
            if (selectedNode != lastKnownSelectedNode && selectedNode != currentNode)
            {
                // User manually selected a different node — reset navigation
                navigationHistory.Clear();
                currentBranches.Clear();
                currentBranchIndex = 0;
                currentNode = selectedNode;
                lastKnownSelectedNode = selectedNode;

                // Don't pre-populate branches — label should show "0" until user navigates
                UpdateUI();
            }
        }

        // ==================== NAVIGATION ACTIONS ====================

        private void NavigateForward()
        {
            if (!TryResolveGraphView()) return;

            // If no current node, try to use the selected node
            if (currentNode == null)
            {
                currentNode = GetSelectedNodeView();
                if (currentNode == null) return;
            }

            // Get forward connections (output-connected nodes)
            var forwardNodes = GetOutputConnectedNodes(currentNode);
            if (forwardNodes.Count == 0) return;

            // Push current node to history
            navigationHistory.Push(currentNode);

            // Navigate to first forward connection
            currentNode = forwardNodes[0];
            lastKnownSelectedNode = currentNode;

            // Store all forward connections as branches for up/down navigation
            currentBranches = forwardNodes;
            currentBranchIndex = 0;

            FocusOnNode(currentNode);
            UpdateUI();
        }

        private void NavigateBack()
        {
            if (!TryResolveGraphView()) return;

            // If no current node, try to use the selected node
            if (currentNode == null)
            {
                currentNode = GetSelectedNodeView();
                if (currentNode == null) return;
            }

            // Get backward connections (input-connected nodes)
            var backwardNodes = GetInputConnectedNodes(currentNode);
            if (backwardNodes.Count == 0) return;

            // Push current node to forward history
            navigationHistory.Push(currentNode);

            // Navigate to first backward connection
            currentNode = backwardNodes[0];
            lastKnownSelectedNode = currentNode;

            // Store all backward connections as branches for up/down navigation
            currentBranches = backwardNodes;
            currentBranchIndex = 0;

            FocusOnNode(currentNode);
            UpdateUI();
        }

        private void NavigateDown()
        {
            if (currentBranches.Count <= 1) return;

            currentBranchIndex = (currentBranchIndex + 1) % currentBranches.Count;
            currentNode = currentBranches[currentBranchIndex];
            lastKnownSelectedNode = currentNode;

            FocusOnNode(currentNode);
            UpdateUI();
        }

        private void NavigateUp()
        {
            if (currentBranches.Count <= 1) return;

            currentBranchIndex = (currentBranchIndex - 1 + currentBranches.Count) % currentBranches.Count;
            currentNode = currentBranches[currentBranchIndex];
            lastKnownSelectedNode = currentNode;

            FocusOnNode(currentNode);
            UpdateUI();
        }

        // ==================== GRAPH TRAVERSAL (Reflection) ====================

        /// <summary>
        /// Gets all nodes connected to the output ports of the given node view.
        /// </summary>
        private List<VisualElement> GetOutputConnectedNodes(VisualElement nodeView)
        {
            var connectedNodes = new List<VisualElement>();
            if (graphView == null || nodeView == null) return connectedNodes;

            try
            {
                // Get all edges from the graph view
                // GraphView has an "edges" property that returns all Edge elements
                var edges = GetEdges();
                if (edges == null) return connectedNodes;

                // Use a set to avoid duplicates
                var seen = new HashSet<VisualElement>();

                foreach (var edge in edges)
                {
                    try
                    {
                        // Each Edge has "output" and "input" Port properties
                        var outputPort = GetEdgePort(edge, "output");
                        var inputPort = GetEdgePort(edge, "input");

                        if (outputPort == null || inputPort == null) continue;

                        // Get the parent node of each port
                        var outputNode = GetPortNode(outputPort);
                        var inputNode = GetPortNode(inputPort);

                        if (outputNode == null || inputNode == null) continue;

                        // If the output side is our node, the input side is a forward connection
                        if (outputNode == nodeView && !seen.Contains(inputNode))
                        {
                            seen.Add(inputNode);
                            connectedNodes.Add(inputNode);
                        }
                    }
                    catch
                    {
                        // Skip problematic edges
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[ShaderGraph Navigator] Error getting output connections: {e.Message}");
            }

            return connectedNodes;
        }

        /// <summary>
        /// Gets all nodes connected to the input ports of the given node view.
        /// </summary>
        private List<VisualElement> GetInputConnectedNodes(VisualElement nodeView)
        {
            var connectedNodes = new List<VisualElement>();
            if (graphView == null || nodeView == null) return connectedNodes;

            try
            {
                var edges = GetEdges();
                if (edges == null) return connectedNodes;

                var seen = new HashSet<VisualElement>();

                foreach (var edge in edges)
                {
                    try
                    {
                        var outputPort = GetEdgePort(edge, "output");
                        var inputPort = GetEdgePort(edge, "input");

                        if (outputPort == null || inputPort == null) continue;

                        var outputNode = GetPortNode(outputPort);
                        var inputNode = GetPortNode(inputPort);

                        if (outputNode == null || inputNode == null) continue;

                        // If the input side is our node, the output side is a backward connection
                        if (inputNode == nodeView && !seen.Contains(outputNode))
                        {
                            seen.Add(outputNode);
                            connectedNodes.Add(outputNode);
                        }
                    }
                    catch
                    {
                        // Skip problematic edges
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[ShaderGraph Navigator] Error getting input connections: {e.Message}");
            }

            return connectedNodes;
        }

        /// <summary>
        /// Gets all Edge elements from the GraphView using reflection.
        /// </summary>
        private List<VisualElement> GetEdges()
        {
            try
            {
                // Query for Edge elements directly from the visual tree
                // This avoids AmbiguousMatchException from the "edges" property and "ToList" method
                var edgeList = new List<VisualElement>();
                graphView.Query<VisualElement>().ForEach(element =>
                {
                    if (element.GetType().Name == "Edge")
                    {
                        edgeList.Add(element);
                    }
                });

                return edgeList;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[ShaderGraph Navigator] Error getting edges: {e.Message}");
                return new List<VisualElement>();
            }
        }

        /// <summary>
        /// Gets a port (output or input) from an Edge element via reflection.
        /// </summary>
        private VisualElement GetEdgePort(VisualElement edge, string portName)
        {
            try
            {
                var portProp = edge.GetType().GetProperty(portName,
                    BindingFlags.Public | BindingFlags.Instance);
                if (portProp != null)
                {
                    return portProp.GetValue(edge) as VisualElement;
                }

                // Try field
                var portField = edge.GetType().GetField(portName,
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (portField != null)
                {
                    return portField.GetValue(edge) as VisualElement;
                }
            }
            catch
            {
                // Silently fail
            }

            return null;
        }

        /// <summary>
        /// Gets the parent node view of a Port element.
        /// Port has a "node" property that returns the parent Node.
        /// </summary>
        private VisualElement GetPortNode(VisualElement port)
        {
            try
            {
                // Port.node property
                var nodeProp = port.GetType().GetProperty("node",
                    BindingFlags.Public | BindingFlags.Instance);
                if (nodeProp != null)
                {
                    return nodeProp.GetValue(port) as VisualElement;
                }

                // Fallback: walk up the visual tree to find a node view
                var parent = port.parent;
                while (parent != null)
                {
                    if (materialNodeViewType != null && materialNodeViewType.IsInstanceOfType(parent))
                        return parent;
                    if (propertyNodeViewType != null && propertyNodeViewType.IsInstanceOfType(parent))
                        return parent;
                    if (parent.GetType().Name == "MaterialNodeView" || parent.GetType().Name == "PropertyNodeView")
                        return parent;

                    parent = parent.parent;
                }
            }
            catch
            {
                // Silently fail
            }

            return null;
        }

        /// <summary>
        /// Gets the currently selected node view from the graph.
        /// </summary>
        private VisualElement GetSelectedNodeView()
        {
            try
            {
                // GraphView has a "selection" property (on the base class)
                var gvType = graphView.GetType();
                var selectionProp = gvType.GetProperty("selection",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.FlattenHierarchy);
                if (selectionProp == null)
                {
                    // Try to find it by scanning all properties
                    var allProps = gvType.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.FlattenHierarchy);
                    foreach (var p in allProps)
                    {
                        if (p.Name.ToLower().Contains("select"))
                        {
                            selectionProp = p;
                            break;
                        }
                    }
                }
                
                if (selectionProp != null)
                {
                    var selection = selectionProp.GetValue(graphView);
                    if (selection is System.Collections.IList list && list.Count > 0)
                    {
                        // The items might be ISelectable, try to cast to VisualElement
                        for (int i = 0; i < list.Count; i++)
                        {
                            var item = list[i];
                            VisualElement element = item as VisualElement;
                            if (element != null && IsNodeView(element))
                            {
                                return element;
                            }
                        }
                    }
                    else if (selection is System.Collections.IEnumerable enumerable)
                    {
                        foreach (var item in enumerable)
                        {
                            VisualElement element = item as VisualElement;
                            if (element != null && IsNodeView(element))
                            {
                                return element;
                            }
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[ShaderGraph Navigator] GetSelectedNodeView error: {e.Message}");
            }

            return null;
        }

        private bool IsNodeView(VisualElement element)
        {
            if (materialNodeViewType != null && materialNodeViewType.IsInstanceOfType(element))
                return true;
            if (propertyNodeViewType != null && propertyNodeViewType.IsInstanceOfType(element))
                return true;

            var typeName = element.GetType().Name;
            return typeName == "MaterialNodeView" || typeName == "PropertyNodeView";
        }

        // ==================== FOCUS ====================

        private void FocusOnNode(VisualElement nodeView)
        {
            if (graphView == null || nodeView == null) return;

            try
            {
                var gvType = graphView.GetType();
                var clearSelectionMethod = gvType.GetMethod("ClearSelection",
                    BindingFlags.Public | BindingFlags.Instance | BindingFlags.FlattenHierarchy);
                clearSelectionMethod?.Invoke(graphView, null);

                var addToSelectionMethod = gvType.GetMethod("AddToSelection",
                    BindingFlags.Public | BindingFlags.Instance | BindingFlags.FlattenHierarchy);
                addToSelectionMethod?.Invoke(graphView, new object[] { nodeView });

                var frameSelectionMethod = gvType.GetMethod("FrameSelection",
                    BindingFlags.Public | BindingFlags.Instance | BindingFlags.FlattenHierarchy);
                if (frameSelectionMethod != null)
                {
                    frameSelectionMethod.Invoke(graphView, null);
                }
                else
                {
                    nodeView.Focus();
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[ShaderGraph Navigator] Could not focus on node: {e.Message}");
            }
        }

        // ==================== UI UPDATE ====================

        private void UpdateUI()
        {
            // Update branch label — show count only after navigating
            if (currentBranches.Count > 0)
            {
                branchLabel.text = $"({currentBranches.Count}/{currentBranchIndex + 1})";
            }
            else
            {
                branchLabel.text = "(0)";
            }

            // Back is enabled if current node has input connections
            bool hasBack = false;
            if (currentNode != null)
            {
                hasBack = GetInputConnectedNodes(currentNode).Count > 0;
            }
            SetButtonEnabled(backButton, hasBack);

            // Forward is enabled if we have a current node with output connections
            bool hasForward = false;
            if (currentNode != null)
            {
                hasForward = GetOutputConnectedNodes(currentNode).Count > 0;
            }
            else
            {
                var selected = GetSelectedNodeView();
                if (selected != null)
                {
                    hasForward = GetOutputConnectedNodes(selected).Count > 0;
                }
            }
            SetButtonEnabled(forwardButton, hasForward);

            // Up/Down enabled if there are multiple branches
            SetButtonEnabled(upButton, currentBranches.Count > 1);
            SetButtonEnabled(downButton, currentBranches.Count > 1);
        }

        private void SetButtonEnabled(Button button, bool enabled)
        {
            button.SetEnabled(enabled);
            button.style.opacity = enabled ? 1f : 0.35f;
        }
    }
}
