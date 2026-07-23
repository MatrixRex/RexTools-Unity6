using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

namespace Rextools.ShaderGraphOrganizer.Editor
{
    /// <summary>
    /// Adds Align and Distribute context menu items to the ShaderGraph editor
    /// for organizing selected nodes in the graph.
    /// </summary>
    [InitializeOnLoad]
    public static class ShaderGraphOrganizerExtension
    {
        private static Type materialGraphEditWindowType;
        private static Type graphEditorViewType;
        private static Type materialGraphViewType;

        // Track which graph views already have the menu attached
        private static readonly HashSet<int> attachedGraphViews = new HashSet<int>();

        static ShaderGraphOrganizerExtension()
        {
            var assembly = Assembly.Load("Unity.ShaderGraph.Editor");
            materialGraphEditWindowType = assembly.GetType("UnityEditor.ShaderGraph.Drawing.MaterialGraphEditWindow");
            graphEditorViewType = assembly.GetType("UnityEditor.ShaderGraph.Drawing.GraphEditorView");
            materialGraphViewType = assembly.GetType("UnityEditor.ShaderGraph.Drawing.MaterialGraphView");

            EditorApplication.update += OnEditorUpdate;
        }

        private static void OnEditorUpdate()
        {
            if (materialGraphEditWindowType == null) return;

            var focused = EditorWindow.focusedWindow;
            if (focused == null || !materialGraphEditWindowType.IsInstanceOfType(focused)) return;

            var windows = Resources.FindObjectsOfTypeAll(materialGraphEditWindowType);
            foreach (var window in windows)
            {
                var editorWindow = window as EditorWindow;
                if (editorWindow == null) continue;

                AttachContextMenu(editorWindow, window);
            }
        }

        private static void AttachContextMenu(EditorWindow editorWindow, object windowInstance)
        {
            // Get GraphEditorView
            var graphEditorViewField = materialGraphEditWindowType.GetField(
                "m_GraphEditorView", BindingFlags.NonPublic | BindingFlags.Instance);
            if (graphEditorViewField == null) return;

            var graphEditorView = graphEditorViewField.GetValue(windowInstance) as VisualElement;
            if (graphEditorView == null) return;

            // Get the graphView from GraphEditorView
            var graphViewProperty = graphEditorViewType.GetProperty(
                "graphView", BindingFlags.Public | BindingFlags.Instance);
            if (graphViewProperty == null) return;

            var graphViewObj = graphViewProperty.GetValue(graphEditorView);
            var graphView = graphViewObj as GraphView;
            if (graphView == null) return;

            int instanceId = graphView.GetHashCode();
            if (attachedGraphViews.Contains(instanceId)) return;
            attachedGraphViews.Add(instanceId);

            // Capture graphView in closure so we always reference the correct GraphView,
            // not evt.target which can be any child element
            graphView.RegisterCallback<ContextualMenuPopulateEvent>(evt =>
            {
                OnContextMenuPopulate(evt, graphView);
            });
        }

        private static void OnContextMenuPopulate(ContextualMenuPopulateEvent evt, GraphView graphView)
        {
            // Get selected nodes directly from the captured GraphView reference
            var selectedNodes = GetSelectedNodes(graphView);
            bool hasMultipleNodes = selectedNodes.Count >= 2;

            // Add separator before our items
            evt.menu.AppendSeparator();

            // Align submenu items
            evt.menu.AppendAction("Align/Left",
                action => AlignNodes(graphView, AlignDirection.Left),
                hasMultipleNodes ? DropdownMenuAction.Status.Normal : DropdownMenuAction.Status.Disabled);

            evt.menu.AppendAction("Align/Right",
                action => AlignNodes(graphView, AlignDirection.Right),
                hasMultipleNodes ? DropdownMenuAction.Status.Normal : DropdownMenuAction.Status.Disabled);

            evt.menu.AppendAction("Align/Up",
                action => AlignNodes(graphView, AlignDirection.Up),
                hasMultipleNodes ? DropdownMenuAction.Status.Normal : DropdownMenuAction.Status.Disabled);

            evt.menu.AppendAction("Align/Down",
                action => AlignNodes(graphView, AlignDirection.Down),
                hasMultipleNodes ? DropdownMenuAction.Status.Normal : DropdownMenuAction.Status.Disabled);

            // Distribute submenu items
            evt.menu.AppendAction("Distribute/Horizontal",
                action => DistributeNodes(graphView, DistributeDirection.Horizontal),
                hasMultipleNodes ? DropdownMenuAction.Status.Normal : DropdownMenuAction.Status.Disabled);

            evt.menu.AppendAction("Distribute/Vertical",
                action => DistributeNodes(graphView, DistributeDirection.Vertical),
                hasMultipleNodes ? DropdownMenuAction.Status.Normal : DropdownMenuAction.Status.Disabled);

            // Add Auto Align Inputs item when right-clicking over a node
            var targetNode = GetNodeFromEventTarget(evt.target as VisualElement);
            if (targetNode != null)
            {
                var inputNodes = GetConnectedInputNodes(targetNode);
                evt.menu.AppendAction("Auto Align Inputs",
                    action => AutoAlignInputs(graphView, targetNode, inputNodes),
                    inputNodes.Count > 0 ? DropdownMenuAction.Status.Normal : DropdownMenuAction.Status.Disabled);
            }
        }

        private static List<Node> GetSelectedNodes(GraphView graphView)
        {
            var nodes = new List<Node>();

            try
            {
                // Access selection directly from the GraphView instance
                var selection = graphView.selection;
                if (selection == null) return nodes;

                foreach (var selectable in selection)
                {
                    if (selectable is Node node)
                    {
                        nodes.Add(node);
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"ShaderGraph Organizer: Could not get selected nodes - {e.Message}");
            }

            return nodes;
        }

        private static void AlignNodes(GraphView graphView, AlignDirection direction)
        {
            var selectedNodes = GetSelectedNodes(graphView);
            if (selectedNodes.Count < 2) return;

            // Record undo
            Undo.IncrementCurrentGroup();
            // We can't directly undo VisualElement positions, but the underlying
            // graph data will be marked dirty by SetPosition

            // Get all rects
            var rects = new Dictionary<Node, Rect>();
            foreach (var node in selectedNodes)
            {
                rects[node] = node.GetPosition();
            }

            switch (direction)
            {
                case AlignDirection.Left:
                {
                    float targetX = rects.Values.Min(r => r.x);
                    foreach (var node in selectedNodes)
                    {
                        var rect = rects[node];
                        rect.x = targetX;
                        node.SetPosition(rect);
                    }
                    break;
                }

                case AlignDirection.Right:
                {
                    float targetRight = rects.Values.Max(r => r.x + r.width);
                    foreach (var node in selectedNodes)
                    {
                        var rect = rects[node];
                        rect.x = targetRight - rect.width;
                        node.SetPosition(rect);
                    }
                    break;
                }

                case AlignDirection.Up:
                {
                    float targetY = rects.Values.Min(r => r.y);
                    foreach (var node in selectedNodes)
                    {
                        var rect = rects[node];
                        rect.y = targetY;
                        node.SetPosition(rect);
                    }
                    break;
                }

                case AlignDirection.Down:
                {
                    float targetBottom = rects.Values.Max(r => r.y + r.height);
                    foreach (var node in selectedNodes)
                    {
                        var rect = rects[node];
                        rect.y = targetBottom - rect.height;
                        node.SetPosition(rect);
                    }
                    break;
                }
            }

            // Mark the graph as dirty so positions are saved
            try
            {
                var markDirtyMethod = materialGraphViewType.GetMethod(
                    "MarkDirtyRepaint", BindingFlags.Public | BindingFlags.Instance);
                markDirtyMethod?.Invoke(graphView, null);
            }
            catch
            {
                // Fallback: repaint via the element
                graphView.MarkDirtyRepaint();
            }
        }

        private const float DistributeGap = 12f;

        private static void DistributeNodes(GraphView graphView, DistributeDirection direction)
        {
            var selectedNodes = GetSelectedNodes(graphView);
            if (selectedNodes.Count < 2) return;

            switch (direction)
            {
                case DistributeDirection.Horizontal:
                {
                    // Sort nodes by X position (left to right)
                    var sorted = selectedNodes
                        .OrderBy(n => n.GetPosition().x)
                        .ToList();

                    // Place each node after the previous one with a gap
                    float currentX = sorted[0].GetPosition().x;
                    foreach (var node in sorted)
                    {
                        var rect = node.GetPosition();
                        rect.x = currentX;
                        node.SetPosition(rect);
                        currentX += rect.width + DistributeGap;
                    }
                    break;
                }

                case DistributeDirection.Vertical:
                {
                    // Sort nodes by Y position (top to bottom)
                    var sorted = selectedNodes
                        .OrderBy(n => n.GetPosition().y)
                        .ToList();

                    // Place each node below the previous one with a gap
                    float currentY = sorted[0].GetPosition().y;
                    foreach (var node in sorted)
                    {
                        var rect = node.GetPosition();
                        rect.y = currentY;
                        node.SetPosition(rect);
                        currentY += rect.height + DistributeGap;
                    }
                    break;
                }
            }

            graphView.MarkDirtyRepaint();
        }

        private static Node GetNodeFromEventTarget(VisualElement target)
        {
            var element = target;
            while (element != null)
            {
                if (element is Node node)
                {
                    return node;
                }
                element = element.parent;
            }
            return null;
        }

        private static List<Node> GetConnectedInputNodes(Node targetNode)
        {
            var inputNodes = new List<Node>();
            if (targetNode == null) return inputNodes;

            var ports = new List<Port>();
            FindPorts(targetNode.inputContainer, ports);

            // Sort ports by their visual vertical position (top to bottom)
            ports.Sort((a, b) => a.worldBound.y.CompareTo(b.worldBound.y));

            foreach (var port in ports)
            {
                if (port.connections == null) continue;
                foreach (var edge in port.connections)
                {
                    var outputPort = edge.output;
                    if (outputPort != null && outputPort.node is Node inputNode)
                    {
                        if (!inputNodes.Contains(inputNode))
                        {
                            inputNodes.Add(inputNode);
                        }
                    }
                }
            }

            return inputNodes;
        }

        private static void FindPorts(VisualElement element, List<Port> ports)
        {
            if (element is Port port)
            {
                ports.Add(port);
            }
            else
            {
                int count = element.childCount;
                for (int i = 0; i < count; i++)
                {
                    FindPorts(element[i], ports);
                }
            }
        }

        private const float AlignSpacing = 80f;

        private static void AutoAlignInputs(GraphView graphView, Node targetNode, List<Node> inputNodes)
        {
            if (targetNode == null || inputNodes == null || inputNodes.Count == 0) return;

            // Record undo
            Undo.IncrementCurrentGroup();

            var targetRect = targetNode.GetPosition();

            // Calculate the total height of all input nodes and gaps
            float totalHeight = 0f;
            foreach (var node in inputNodes)
            {
                totalHeight += node.GetPosition().height;
            }
            totalHeight += (inputNodes.Count - 1) * DistributeGap;

            // Calculate starting Y position to center the input nodes relative to the target node
            float targetCenterY = targetRect.y + (targetRect.height / 2f);
            float currentY = targetCenterY - (totalHeight / 2f);

            // Align and distribute each input node
            foreach (var node in inputNodes)
            {
                var rect = node.GetPosition();
                rect.x = targetRect.x - rect.width - AlignSpacing;
                rect.y = currentY;
                node.SetPosition(rect);
                currentY += rect.height + DistributeGap;
            }

            // Update selection to select only the aligned input nodes
            graphView.ClearSelection();
            foreach (var node in inputNodes)
            {
                graphView.AddToSelection(node);
            }

            // Mark the graph as dirty so positions are saved
            try
            {
                var markDirtyMethod = materialGraphViewType.GetMethod(
                    "MarkDirtyRepaint", BindingFlags.Public | BindingFlags.Instance);
                markDirtyMethod?.Invoke(graphView, null);
            }
            catch
            {
                graphView.MarkDirtyRepaint();
            }
        }

        private enum AlignDirection
        {
            Left,
            Right,
            Up,
            Down
        }

        private enum DistributeDirection
        {
            Horizontal,
            Vertical
        }
    }
}
