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
