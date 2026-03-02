using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using Cursor = UnityEngine.UIElements.Cursor;

namespace Rextools.ShaderGraphSearch.Editor
{
    
    [InitializeOnLoad]
    public static class ShaderGraphSearchExtension
    {
        private static Type materialGraphEditWindowType;
        private static Type graphEditorViewType;
        private static Type materialGraphViewType;
        private static Type materialNodeViewType;
        private static Type propertyNodeViewType;
        private static Type blackboardFieldType;
        private static Type sgBlackboardFieldType;

        // Navigation state for VS Code-style search
        private static List<GroupedSearchResult> currentGroupedResults = new List<GroupedSearchResult>();
        private static GroupedSearchResult selectedGroup = null;
        private static int currentIndexInGroup = 0;
        private static VisualElement currentGraphView = null;
        private static Label counterLabel = null;
        private static ToolbarSearchField currentSearchField = null;
        private static bool isUpdatingSearchFieldProgrammatically = false;

        // Connection navigator
        private static ShaderGraphNodeNavigator nodeNavigator = null;

        static ShaderGraphSearchExtension()
        {
            // Get types via reflection since they're internal
            var assembly = Assembly.Load("Unity.ShaderGraph.Editor");
            materialGraphEditWindowType = assembly.GetType("UnityEditor.ShaderGraph.Drawing.MaterialGraphEditWindow");
            graphEditorViewType = assembly.GetType("UnityEditor.ShaderGraph.Drawing.GraphEditorView");
            materialGraphViewType = assembly.GetType("UnityEditor.ShaderGraph.Drawing.MaterialGraphView");
            materialNodeViewType = assembly.GetType("UnityEditor.ShaderGraph.Drawing.MaterialNodeView");
            propertyNodeViewType = assembly.GetType("UnityEditor.ShaderGraph.Drawing.PropertyNodeView");
            sgBlackboardFieldType = assembly.GetType("UnityEditor.ShaderGraph.Drawing.SGBlackboardField");

            // Try to get the generic blackboard field type
            var graphViewAssembly = typeof(UnityEditor.Experimental.GraphView.GraphView).Assembly;
            blackboardFieldType = graphViewAssembly.GetType("UnityEditor.Experimental.GraphView.BlackboardField");

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

                if (editorWindow.rootVisualElement.Q<VisualElement>("search-container") == null)
                {
                    AddSearchBar(editorWindow, window);
                }
            }

            // Poll for selection changes to update the connection navigator
            nodeNavigator?.CheckSelectionChanged();
        }

        private static void AddSearchBar(EditorWindow editorWindow, object windowInstance)
        {
            var root = editorWindow.rootVisualElement;

            // Get GraphEditorView using reflection
            var graphEditorViewField = materialGraphEditWindowType.GetField("m_GraphEditorView", BindingFlags.NonPublic | BindingFlags.Instance);
            if (graphEditorViewField == null) return;

            var graphEditorView = graphEditorViewField.GetValue(windowInstance) as VisualElement;
            if (graphEditorView == null) return;

            // Try to find the toolbar in the GraphEditorView
            VisualElement toolbar = graphEditorView.Q(className: "unity-toolbar");
            if (toolbar == null) toolbar = graphEditorView.Q("toolbar");
            if (toolbar == null) toolbar = graphEditorView.Q(className: "toolbar");
            
            // Also try to find any toolbar-like element at the top
            if (toolbar == null)
            {
                graphEditorView.Query<VisualElement>().ForEach(element =>
                {
                    var typeName = element.GetType().Name;
                    if ((typeName.Contains("Toolbar") || typeName.Contains("toolbar")) && toolbar == null)
                    {
                        toolbar = element;
                    }
                });
            }

            // ==================== SEARCH CONTAINER ====================
            var searchContainer = new VisualElement { name = "search-container" };
            searchContainer.style.flexDirection = FlexDirection.Row;
            searchContainer.style.alignItems = Align.Center;
            searchContainer.style.marginLeft = 0;         // LEFT MARGIN: gap from nav buttons
            searchContainer.style.marginRight = 5;      // RIGHT MARGIN: gap from right edge

            // ==================== SEARCH FIELD ====================
            var searchField = new ToolbarSearchField { name = "node-search-field" };
            currentSearchField = searchField;  // Store reference for SelectGroup
            searchField.style.width = 150;              // WIDTH: default width of search field
            searchField.style.minWidth = 100;           // MIN WIDTH: minimum before it can't shrink more
            searchField.style.height = 12;              // HEIGHT: search field height
            searchField.style.flexShrink = 1;           // FLEX SHRINK: 1 = allows shrinking, 0 = fixed
            searchField.style.flexGrow = 0;             // FLEX GROW: 0 = don't expand

            // ==================== NAV BUTTONS CONTAINER ====================
            var navContainer = new VisualElement();
            navContainer.style.flexDirection = FlexDirection.Row;
            navContainer.style.marginLeft = 1;          // BUTTON GAP: space between search field and buttons
            navContainer.style.alignItems = Align.Center;
            navContainer.style.flexShrink = 0;          // Don't shrink buttons

            // ==================== BUTTON STYLING ====================
            void StyleToolbarButton(Button btn, Image icon)
            {
                // BUTTON SIZE
                btn.style.width = 22;                   // BUTTON WIDTH
                btn.style.height = 20;                  // BUTTON HEIGHT
                
                // BUTTON MARGINS (spacing between buttons)
                btn.style.marginLeft = 1;               // LEFT GAP between buttons
                btn.style.marginRight = 1;              // RIGHT GAP between buttons
                
                // BUTTON PADDING (space inside button around icon)
                btn.style.paddingTop = 3;               // TOP PADDING
                btn.style.paddingBottom = 3;            // BOTTOM PADDING
                btn.style.paddingLeft = 4;              // LEFT PADDING
                btn.style.paddingRight = 4;             // RIGHT PADDING
                
                // BORDER WIDTH (0 = no border, 1 = thin line)
                btn.style.borderTopWidth = 0;           // TOP BORDER: 0 = removed
                btn.style.borderBottomWidth = 0;        // BOTTOM BORDER: 0 = removed
                btn.style.borderLeftWidth = 1;          // LEFT BORDER: 1 = visible
                btn.style.borderRightWidth = 1;         // RIGHT BORDER: 1 = visible
                
                // BORDER COLORS (RGB 0-1)
                btn.style.borderTopColor = new Color(0.15f, 0.15f, 0.15f);
                btn.style.borderBottomColor = new Color(0.15f, 0.15f, 0.15f);
                btn.style.borderLeftColor = new Color(0.15f, 0.15f, 0.15f);   // LEFT BORDER COLOR
                btn.style.borderRightColor = new Color(0.15f, 0.15f, 0.15f);  // RIGHT BORDER COLOR
                
                // BORDER RADIUS (0 = square corners)
                btn.style.borderTopLeftRadius = 0;      // TOP-LEFT CORNER: 0 = square
                btn.style.borderTopRightRadius = 0;     // TOP-RIGHT CORNER: 0 = square
                btn.style.borderBottomLeftRadius = 0;   // BOTTOM-LEFT CORNER: 0 = square
                btn.style.borderBottomRightRadius = 0;  // BOTTOM-RIGHT CORNER: 0 = square
                
                // BACKGROUND COLOR (RGB 0-1, higher = lighter)
                btn.style.backgroundColor = new Color(0.35f, 0.35f, 0.35f);
                
                // ICON SIZE
                icon.style.width = 12;                  // ICON WIDTH
                icon.style.height = 12;                 // ICON HEIGHT
                icon.scaleMode = ScaleMode.ScaleToFit;
                icon.style.alignSelf = Align.Center;
            }

            // ==================== UP BUTTON ====================
            var upButton = new Button { name = "search-up-btn" };
            var upIcon = new Image();
            upIcon.image = EditorGUIUtility.IconContent("d_scrollup").image;
            upButton.Add(upIcon);
            StyleToolbarButton(upButton, upIcon);

            // ==================== DOWN BUTTON ====================
            var downButton = new Button { name = "search-down-btn" };
            var downIcon = new Image();
            downIcon.image = EditorGUIUtility.IconContent("d_scrolldown").image;
            downButton.Add(downIcon);
            StyleToolbarButton(downButton, downIcon);

            // ==================== COUNTER LABEL (e.g. "1 of 3") ====================
            counterLabel = new Label { name = "search-counter" };
            counterLabel.style.fontSize = 11;                    // FONT SIZE
            counterLabel.style.color = new Color(0.7f, 0.7f, 0.7f); // TEXT COLOR
            counterLabel.style.marginLeft = 0;                   // LEFT MARGIN
            counterLabel.style.marginRight = 0;                  // RIGHT MARGIN
            counterLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
            counterLabel.style.minWidth = 50;                    // MIN WIDTH to prevent jumping
            counterLabel.text = "0/0";  // Initially empty

            navContainer.Add(counterLabel);
            navContainer.Add(upButton);
            navContainer.Add(downButton);

            // Create search results dropdown
            var resultsContainer = new VisualElement { name = "search-results" };
            resultsContainer.style.position = Position.Absolute;
            resultsContainer.style.top = 22;
            resultsContainer.style.left = 0;
            resultsContainer.style.width = 300;
            resultsContainer.style.maxHeight = 400;
            resultsContainer.style.backgroundColor = new Color(0.2f, 0.2f, 0.2f, 0.98f);
            resultsContainer.style.borderTopWidth = 1;
            resultsContainer.style.borderBottomWidth = 1;
            resultsContainer.style.borderLeftWidth = 1;
            resultsContainer.style.borderRightWidth = 1;
            resultsContainer.style.borderTopColor = new Color(0.1f, 0.1f, 0.1f);
            resultsContainer.style.borderBottomColor = new Color(0.1f, 0.1f, 0.1f);
            resultsContainer.style.borderLeftColor = new Color(0.1f, 0.1f, 0.1f);
            resultsContainer.style.borderRightColor = new Color(0.1f, 0.1f, 0.1f);
            resultsContainer.style.borderBottomLeftRadius = 4;
            resultsContainer.style.borderBottomRightRadius = 4;
            resultsContainer.style.display = DisplayStyle.None;

            var scrollView = new ScrollView { name = "results-scroll" };
            scrollView.style.maxHeight = 400;
            resultsContainer.Add(scrollView);

            // Register search callback
            searchField.RegisterValueChangedCallback(evt =>
            {
                // Skip if we're programmatically updating the search field
                if (isUpdatingSearchFieldProgrammatically) return;
                
                // Clear navigation state when search changes
                selectedGroup = null;
                currentIndexInGroup = 0;
                UpdateCounterLabel();
                
                UpdateSearchResults(graphEditorView, evt.newValue, scrollView, resultsContainer);
                resultsContainer.style.display = string.IsNullOrEmpty(evt.newValue) ? DisplayStyle.None : DisplayStyle.Flex;
            });

            // Also handle the text field specifically
            var textField = searchField.Q<TextField>();
            if (textField != null)
            {
                textField.RegisterCallback<BlurEvent>(evt =>
                {
                    EditorApplication.delayCall += () =>
                    {
                        resultsContainer.style.display = DisplayStyle.None;
                    };
                });
            }

            // Navigation buttons for cycling through results
            upButton.clicked += () => NavigateToPrevious();
            downButton.clicked += () => NavigateToNext();

            searchContainer.Add(searchField);
            searchContainer.Add(navContainer);
            searchContainer.Add(resultsContainer);

            // ==================== CONNECTION NAVIGATION CONTAINER ====================
            var connNavContainer = new VisualElement { name = "conn-nav-container" };
            connNavContainer.style.flexDirection = FlexDirection.Row;
            connNavContainer.style.alignItems = Align.Center;
            connNavContainer.style.flexShrink = 0;
            connNavContainer.style.marginRight = 50;     // Gap before search container

            // --- Back Button ---
            var connBackButton = new Button { name = "conn-back-btn" };
            var connBackIcon = new Image();
            connBackIcon.image = EditorGUIUtility.IconContent("d_tab_prev").image;
            connBackButton.Add(connBackIcon);
            StyleToolbarButton(connBackButton, connBackIcon);

            // --- Forward Button ---
            var connForwardButton = new Button { name = "conn-forward-btn" };
            var connForwardIcon = new Image();
            connForwardIcon.image = EditorGUIUtility.IconContent("d_tab_next").image;
            connForwardButton.Add(connForwardIcon);
            StyleToolbarButton(connForwardButton, connForwardIcon);

            // --- Separator 1 ---
            var sep1 = CreateSeparator();

            // --- Up Button ---
            var connUpButton = new Button { name = "conn-up-btn" };
            var connUpIcon = new Image();
            connUpIcon.image = EditorGUIUtility.IconContent("d_scrollup").image;
            connUpButton.Add(connUpIcon);
            StyleToolbarButton(connUpButton, connUpIcon);

            // --- Down Button ---
            var connDownButton = new Button { name = "conn-down-btn" };
            var connDownIcon = new Image();
            connDownIcon.image = EditorGUIUtility.IconContent("d_scrolldown").image;
            connDownButton.Add(connDownIcon);
            StyleToolbarButton(connDownButton, connDownIcon);

            // --- Branch Label ---
            var connBranchLabel = new Label { name = "conn-branch-label" };
            connBranchLabel.style.fontSize = 11;
            connBranchLabel.style.color = new Color(0.7f, 0.7f, 0.7f);
            connBranchLabel.style.marginLeft = 2;
            connBranchLabel.style.marginRight = 2;
            connBranchLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
            connBranchLabel.style.minWidth = 35;
            connBranchLabel.text = "(-/-)";

            // --- Separator 2 ---
            var sep2 = CreateSeparator();

            connNavContainer.Add(connBackButton);
            connNavContainer.Add(connForwardButton);
            connNavContainer.Add(sep1);
            connNavContainer.Add(connUpButton);
            connNavContainer.Add(connDownButton);
            connNavContainer.Add(connBranchLabel);
            connNavContainer.Add(sep2);

            // ==================== CREATE NAVIGATOR INSTANCE ====================
            nodeNavigator = new ShaderGraphNodeNavigator(
                materialGraphViewType,
                materialNodeViewType,
                propertyNodeViewType,
                graphEditorViewType,
                connBackButton,
                connForwardButton,
                connUpButton,
                connDownButton,
                connBranchLabel);
            nodeNavigator.SetGraphEditorView(graphEditorView);

            // Try to inject into toolbar if found, otherwise use absolute positioning
            if (toolbar != null)
            {
                // Insert into toolbar with flexGrow to push it to the right
                var spacer = new VisualElement();
                spacer.style.flexGrow = 1;
                toolbar.Add(spacer);
                toolbar.Add(connNavContainer);
                toolbar.Add(searchContainer);
            }
            else
            {
                // Fallback: absolute positioning
                searchContainer.style.position = Position.Absolute;
                searchContainer.style.top = -0.5f;
                searchContainer.style.right = 350;
                root.Add(searchContainer);

                // Also add the connection navigator in absolute mode
                connNavContainer.style.position = Position.Absolute;
                connNavContainer.style.top = -0.5f;
                connNavContainer.style.right = 550;
                root.Add(connNavContainer);
            }
        }

        private static void UpdateSearchResults(VisualElement graphEditorView, string query, ScrollView scrollView, VisualElement resultsContainer)
        {
            scrollView.Clear();

            if (string.IsNullOrEmpty(query) || query.Length < 2)
                return;

            // Get graphView from graphEditorView
            var graphViewProperty = graphEditorViewType.GetProperty("graphView", BindingFlags.Public | BindingFlags.Instance);
            if (graphViewProperty == null) return;

            var graphView = graphViewProperty.GetValue(graphEditorView) as VisualElement;
            if (graphView == null) return;

            var results = SearchNodes(graphView, query);

            if (results.Count == 0)
            {
                var noResultLabel = new Label("No results found");
                noResultLabel.style.paddingTop = 5;
                noResultLabel.style.paddingBottom = 5;
                noResultLabel.style.paddingLeft = 10;
                noResultLabel.style.paddingRight = 10;
                noResultLabel.style.color = new Color(0.6f, 0.6f, 0.6f);
                scrollView.Add(noResultLabel);
                currentGroupedResults.Clear();
                return;
            }

            // Store graphView reference for navigation
            currentGraphView = graphView;
            nodeNavigator?.SetGraphView(graphView);

            // Group results by DisplayName + NodeType
            var grouped = results
                .GroupBy(r => r.DisplayName + "|" + r.NodeType)
                .Select(g => new GroupedSearchResult
                {
                    DisplayName = g.First().DisplayName,
                    NodeType = g.First().NodeType,
                    Instances = g.ToList()
                })
                .OrderBy(g => g.DisplayName)
                .ToList();

            currentGroupedResults = grouped;

            foreach (var group in grouped)
            {
                var resultItem = CreateGroupedResultItem(group, graphView, resultsContainer);
                scrollView.Add(resultItem);
            }
        }

        // ==================== SEPARATOR HELPER ====================

        private static VisualElement CreateSeparator()
        {
            var separator = new VisualElement();
            separator.style.width = 1;
            separator.style.height = 16;
            separator.style.backgroundColor = new Color(0.15f, 0.15f, 0.15f);
            separator.style.marginLeft = 4;
            separator.style.marginRight = 4;
            separator.style.alignSelf = Align.Center;
            return separator;
        }

        // ==================== NAVIGATION METHODS ====================
        
        private static void UpdateCounterLabel()
        {
            if (counterLabel == null) return;
            
            if (selectedGroup == null || selectedGroup.Count == 0)
            {
                counterLabel.text = "0/0";
            }
            else
            {
                counterLabel.text = $"{currentIndexInGroup + 1} / {selectedGroup.Count}";
            }
        }

        private static void NavigateToNext()
        {
            if (selectedGroup == null || selectedGroup.Count == 0) return;
            
            currentIndexInGroup = (currentIndexInGroup + 1) % selectedGroup.Count;
            UpdateCounterLabel();
            
            // Focus on the node at current index
            var result = selectedGroup.Instances[currentIndexInGroup];
            if (currentGraphView != null)
            {
                FocusOnNode(currentGraphView, result.NodeView);
            }
        }

        private static void NavigateToPrevious()
        {
            if (selectedGroup == null || selectedGroup.Count == 0) return;
            
            currentIndexInGroup = (currentIndexInGroup - 1 + selectedGroup.Count) % selectedGroup.Count;
            UpdateCounterLabel();
            
            // Focus on the node at current index
            var result = selectedGroup.Instances[currentIndexInGroup];
            if (currentGraphView != null)
            {
                FocusOnNode(currentGraphView, result.NodeView);
            }
        }

        private static void SelectGroup(GroupedSearchResult group, VisualElement resultsContainer)
        {
            selectedGroup = group;
            currentIndexInGroup = 0;
            UpdateCounterLabel();
            
            // Update search field text with selected group name
            // Use SetValueWithoutNotify to avoid triggering the callback
            if (currentSearchField != null)
            {
                // Get the internal text field and set value without notification
                var textField = currentSearchField.Q<TextField>();
                if (textField != null)
                {
                    textField.SetValueWithoutNotify(group.DisplayName);
                }
            }
            
            // Focus on first instance
            if (group.Instances.Count > 0 && currentGraphView != null)
            {
                FocusOnNode(currentGraphView, group.Instances[0].NodeView);
            }
            
            // Hide dropdown
            resultsContainer.style.display = DisplayStyle.None;
        }

        // ==================== GROUPED RESULT ITEM ====================
        
        private static VisualElement CreateGroupedResultItem(GroupedSearchResult group, VisualElement graphView, VisualElement resultsContainer)
        {
            var item = new VisualElement();
            item.style.flexDirection = FlexDirection.Row;
            item.style.paddingTop = 6;
            item.style.paddingBottom = 6;
            item.style.paddingLeft = 10;
            item.style.paddingRight = 10;
            item.style.backgroundColor = new Color(0.25f, 0.25f, 0.25f, 0);

            // Hover effect
            item.RegisterCallback<MouseEnterEvent>(evt =>
            {
                item.style.backgroundColor = new Color(0.3f, 0.5f, 0.8f, 0.3f);
            });
            item.RegisterCallback<MouseLeaveEvent>(evt =>
            {
                item.style.backgroundColor = new Color(0.25f, 0.25f, 0.25f, 0);
            });

            var labelContainer = new VisualElement();
            labelContainer.style.flexGrow = 1;
            labelContainer.style.flexDirection = FlexDirection.Row;
            labelContainer.style.alignItems = Align.Center;

            // Name label
            var nameLabel = new Label(group.DisplayName);
            nameLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            nameLabel.style.fontSize = 12;
            nameLabel.style.color = Color.white;

            // Count badge if more than 1
            if (group.Count > 1)
            {
                var countLabel = new Label($" [{group.Count}]");
                countLabel.style.fontSize = 11;
                countLabel.style.color = new Color(0.5f, 0.8f, 1f);  // Light blue for count
                labelContainer.Add(nameLabel);
                labelContainer.Add(countLabel);
            }
            else
            {
                labelContainer.Add(nameLabel);
            }

            // Type label on the right
            var typeLabel = new Label(group.NodeType);
            typeLabel.style.fontSize = 10;
            typeLabel.style.color = new Color(0.6f, 0.6f, 0.6f);
            typeLabel.style.marginLeft = 10;

            item.Add(labelContainer);
            item.Add(typeLabel);

            // Click to select this group
            item.RegisterCallback<MouseDownEvent>(evt =>
            {
                SelectGroup(group, resultsContainer);
                evt.StopPropagation();
            });

            return item;
        }

        private static List<NodeSearchResult> SearchNodes(VisualElement graphView, string query)
        {
            var results = new List<NodeSearchResult>();
            query = query.ToLower();

            // Search regular nodes
            var nodes = new List<VisualElement>();
            var allElementTypes = new HashSet<string>();

            // First, log ALL element types in the graph for debugging
            graphView.Query<VisualElement>().ForEach(element =>
            {
                var typeName = element.GetType().Name;
                if (!allElementTypes.Contains(typeName))
                {
                    allElementTypes.Add(typeName);
                }
            });
            
            // Debug.Log($"[DEBUG] All element types in graph: {string.Join(", ", allElementTypes)}");
            // Debug.Log($"[DEBUG] propertyNodeViewType loaded: {propertyNodeViewType != null}, materialNodeViewType loaded: {materialNodeViewType != null}");

            // Use HashSet to avoid duplicates
            var nodeSet = new HashSet<VisualElement>();

            // Approach 1: Query by MaterialNodeView type (regular nodes)
            if (materialNodeViewType != null)
            {
                graphView.Query<VisualElement>().ForEach(element =>
                {
                    if (materialNodeViewType.IsInstanceOfType(element))
                    {
                        nodeSet.Add(element);
                    }
                });
            }

            // Approach 2: Query by PropertyNodeView type (property/variable nodes)
            if (propertyNodeViewType != null)
            {
                graphView.Query<VisualElement>().ForEach(element =>
                {
                    if (propertyNodeViewType.IsInstanceOfType(element))
                    {
                        nodeSet.Add(element);
                        // Debug.Log($"[PROPERTY NODE FOUND via type] Type: {element.GetType().Name}");
                    }
                });
            }
            
            // Approach 2b: Fallback - find PropertyNodeView by type name (in case reflection type doesn't match)
            graphView.Query<VisualElement>().ForEach(element =>
            {
                var typeName = element.GetType().Name;
                if (typeName == "PropertyNodeView" && !nodeSet.Contains(element))
                {
                    nodeSet.Add(element);
                    // Debug.Log($"[PROPERTY NODE FOUND via name match] Type: {typeName}");
                    
                    // Debug.Log($"[PROPERTY NODE] FullName: {element.GetType().FullName}, Assembly: {element.GetType().Assembly.GetName().Name}");
                }
            });

            // Approach 2c: Find ShaderGroup elements (groups in the graph)
            var groups = new List<VisualElement>();
            graphView.Query<VisualElement>().ForEach(element =>
            {
                var typeName = element.GetType().Name;
                if (typeName == "ShaderGroup")
                {
                    groups.Add(element);
                    // Debug.Log($"[GROUP FOUND] Type: {typeName}");
                }
            });

            nodes.AddRange(nodeSet);

            // Approach 3: Fallback to class name if type query found nothing
            if (nodes.Count == 0)
            {
                graphView.Query(className: "node").ForEach(node => nodes.Add(node));
            }

            // Approach 4: Try other potential class names for property nodes
            if (nodes.Count == 0)
            {
                graphView.Query(className: "graphElement").ForEach(node => nodes.Add(node));
            }

            // Debug.Log($"ShaderGraph Search: Found {nodes.Count} node views, {groups.Count} groups");

            // Process regular nodes
            foreach (var nodeView in nodes)
            {
                try
                {
                    string nodeName = "";
                    string nodeTitle = "";
                    string nodeTypeName = "";
                    object node = null;
                    bool isPropertyNode = false;

                    // Try to get the node data object from MaterialNodeView
                    if (materialNodeViewType != null && materialNodeViewType.IsInstanceOfType(nodeView))
                    {
                        var nodeProperty = materialNodeViewType.GetProperty("node", BindingFlags.Public | BindingFlags.Instance);
                        if (nodeProperty != null)
                        {
                            node = nodeProperty.GetValue(nodeView);
                            if (node != null)
                            {
                                // Get node name
                                var nameProperty = node.GetType().GetProperty("name", BindingFlags.Public | BindingFlags.Instance);
                                nodeName = nameProperty?.GetValue(node) as string ?? "";

                                nodeTypeName = node.GetType().Name.Replace("Node", "");
                            }
                        }
                    }
                    // Try to get the node data object from PropertyNodeView
                    // Check both by reflection type AND by type name (in case reflection doesn't match)
                    else if ((propertyNodeViewType != null && propertyNodeViewType.IsInstanceOfType(nodeView)) ||
                             nodeView.GetType().Name == "PropertyNodeView")
                    {
                        isPropertyNode = true;
                        nodeTypeName = "Variable";
                        
                        // Try to get the node property - search up the type hierarchy
                        PropertyInfo nodeProperty = null;
                        var viewType = nodeView.GetType();
                        while (viewType != null && nodeProperty == null)
                        {
                            nodeProperty = viewType.GetProperty("node", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                            viewType = viewType.BaseType;
                        }
                        
                        if (nodeProperty != null)
                        {
                            node = nodeProperty.GetValue(nodeView);
                            if (node != null)
                            {
                                // Debug.Log($"[PROPERTY NODE] Node object type: {node.GetType().Name}");
                                
                                // Get node name
                                var nameProperty = node.GetType().GetProperty("name", BindingFlags.Public | BindingFlags.Instance);
                                nodeName = nameProperty?.GetValue(node) as string ?? "";
                                
                                // Try to get property reference for display name
                                var propertyProp = node.GetType().GetProperty("property", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                                if (propertyProp != null)
                                {
                                    var shaderInput = propertyProp.GetValue(node);
                                    if (shaderInput != null)
                                    {
                                        // Debug.Log($"[PROPERTY NODE] ShaderInput type: {shaderInput.GetType().Name}");
                                        
                                        var displayNameProp = shaderInput.GetType().GetProperty("displayName", BindingFlags.Public | BindingFlags.Instance);
                                        if (displayNameProp != null)
                                        {
                                            nodeName = displayNameProp.GetValue(shaderInput) as string ?? nodeName;
                                        }
                                    }
                                }
                            }
                        }
                        
                        // If we still don't have a name, get it from the view's title
                        if (string.IsNullOrEmpty(nodeName))
                        {
                            var titleProp = nodeView.GetType().GetProperty("title", BindingFlags.Public | BindingFlags.Instance);
                            if (titleProp != null)
                            {
                                nodeName = titleProp.GetValue(nodeView) as string ?? "";
                            }
                        }
                    }

                    // Get the title for logging
                    string logTitle = "";
                    var logTitleLabel = nodeView.Q<Label>("title-label");
                    if (logTitleLabel == null) logTitleLabel = nodeView.Q<Label>("title");
                    if (logTitleLabel != null) logTitle = logTitleLabel.text ?? "";
                    
                    // Also try view title property
                    if (string.IsNullOrEmpty(logTitle))
                    {
                        var titleProp = nodeView.GetType().GetProperty("title", BindingFlags.Public | BindingFlags.Instance);
                        if (titleProp != null) logTitle = titleProp.GetValue(nodeView) as string ?? "";
                    }

                    // Log EVERY node with its FULL type name (not trimmed) for debugging
                    // string fullTypeName = node?.GetType().Name ?? "Unknown";
                    // Debug.Log($"[NODE] Type: '{fullTypeName}', Name: '{nodeName}', Title: '{logTitle}'");

                    // Try multiple ways to find the title label
                    Label titleLabel = nodeView.Q<Label>("title-label");
                    if (titleLabel == null) titleLabel = nodeView.Q<Label>("title");
                    if (titleLabel == null) titleLabel = nodeView.Q<Label>();

                    // Also check the title property directly
                    if (titleLabel != null)
                    {
                        nodeTitle = titleLabel.text ?? "";
                    }
                    else
                    {
                        // Try to get title from nodeView's title property
                        var titleProperty = nodeView.GetType().GetProperty("title", BindingFlags.Public | BindingFlags.Instance);
                        if (titleProperty != null)
                        {
                            nodeTitle = titleProperty.GetValue(nodeView) as string ?? "";
                        }
                    }

                    // Search in both name and title
                    string searchName = nodeName.ToLower();
                    string searchTitle = nodeTitle.ToLower();
                    string searchType = nodeTypeName.ToLower();

                    if (searchName.Contains(query) || searchTitle.Contains(query) || searchType.Contains(query))
                    {
                        string displayName = !string.IsNullOrEmpty(nodeName) ? nodeName :
                                           !string.IsNullOrEmpty(nodeTitle) ? nodeTitle :
                                           nodeTypeName;

                        // Debug.Log($"Match found - Name: '{nodeName}', Title: '{nodeTitle}', Type: '{nodeTypeName}'");

                        results.Add(new NodeSearchResult
                        {
                            NodeView = nodeView,
                            Node = node,
                            DisplayName = displayName,
                            NodeType = nodeTypeName,
                            IsVariable = isPropertyNode
                        });
                    }
                }
                catch (Exception e)
                {
                    // Debug.LogWarning($"ShaderGraph Search: Error processing node - {e.Message}");
                }
            }

            // Process groups
            foreach (var groupView in groups)
            {
                try
                {
                    string groupTitle = "";
                    
                    // Try to get the title from the group
                    var titleLabel = groupView.Q<Label>("titleLabel");
                    if (titleLabel == null) titleLabel = groupView.Q<Label>("title-label");
                    if (titleLabel == null) titleLabel = groupView.Q<Label>("title");
                    if (titleLabel == null) titleLabel = groupView.Q<Label>();
                    
                    if (titleLabel != null)
                    {
                        groupTitle = titleLabel.text ?? "";
                    }
                    
                    // Also try the title property
                    if (string.IsNullOrEmpty(groupTitle))
                    {
                        var titleProp = groupView.GetType().GetProperty("title", BindingFlags.Public | BindingFlags.Instance);
                        if (titleProp != null)
                        {
                            groupTitle = titleProp.GetValue(groupView) as string ?? "";
                        }
                    }
                    

                    
                    if (!string.IsNullOrEmpty(groupTitle) && groupTitle.ToLower().Contains(query))
                    {
                        results.Add(new NodeSearchResult
                        {
                            NodeView = groupView,
                            Node = null,
                            DisplayName = groupTitle,
                            NodeType = "Group",
                            IsVariable = false
                        });
                    }
                }
                catch (Exception e)
                {

                }
            }

            // Search blackboard properties/variables
            var blackboardVariables = SearchBlackboardVariables(graphView, query);
            results.AddRange(blackboardVariables);


            return results.OrderBy(r => r.DisplayName).ToList();
        }

        private static List<NodeSearchResult> SearchBlackboardVariables(VisualElement graphView, string query)
        {
            var results = new List<NodeSearchResult>();

            try
            {
                // Search for property nodes in the graph that reference blackboard variables
                var nodes = new List<VisualElement>();

                // Find all nodes in the graph
                graphView.Query(className: "node").ForEach(node => nodes.Add(node));

                if (nodes.Count == 0 && materialNodeViewType != null)
                {
                    graphView.Query<VisualElement>().ForEach(element =>
                    {
                        if (materialNodeViewType.IsInstanceOfType(element))
                        {
                            nodes.Add(element);
                        }
                    });
                }



                foreach (var nodeView in nodes)
                {
                    try
                    {
                        if (materialNodeViewType == null || !materialNodeViewType.IsInstanceOfType(nodeView))
                            continue;

                        // Get the node data object
                        var nodeProperty = materialNodeViewType.GetProperty("node", BindingFlags.Public | BindingFlags.Instance);
                        if (nodeProperty == null) continue;

                        var node = nodeProperty.GetValue(nodeView);
                        if (node == null) continue;

                        var nodeType = node.GetType();
                        var nodeTypeName = nodeType.Name;

                        // Log all node types for debugging
                        // Debug.Log($"Node type found: {nodeTypeName}");

                        // PropertyNode is the actual variable/input node type in ShaderGraph
                        // These are the nodes you drag from the blackboard
                        bool isPropertyNode = nodeTypeName == "PropertyNode" || 
                                             nodeTypeName.EndsWith("PropertyNode") ||
                                             nodeTypeName.Contains("Property");

                        if (isPropertyNode)
                        {
                            string propertyName = "";
                            string propertyDisplayName = "";

                            // Try to get the property reference from the node
                            // PropertyNode has a "property" field that references the ShaderInput
                            var propertyField = nodeType.GetField("property", BindingFlags.NonPublic | BindingFlags.Instance);
                            if (propertyField == null)
                            {
                                propertyField = nodeType.GetProperty("property", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)?.GetMethod != null
                                    ? null : nodeType.GetField("m_Property", BindingFlags.NonPublic | BindingFlags.Instance);
                            }

                            // Try property getter
                            var propertyProp = nodeType.GetProperty("property", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                            
                            object shaderInput = null;
                            if (propertyProp != null)
                            {
                                shaderInput = propertyProp.GetValue(node);
                            }
                            else if (propertyField != null)
                            {
                                shaderInput = propertyField.GetValue(node);
                            }

                            if (shaderInput != null)
                            {
                                // Get the display name from the ShaderInput
                                var displayNameProp = shaderInput.GetType().GetProperty("displayName", BindingFlags.Public | BindingFlags.Instance);
                                if (displayNameProp != null)
                                {
                                    propertyDisplayName = displayNameProp.GetValue(shaderInput) as string ?? "";
                                }

                                // Also try referenceName
                                var refNameProp = shaderInput.GetType().GetProperty("referenceName", BindingFlags.Public | BindingFlags.Instance);
                                if (refNameProp != null)
                                {
                                    propertyName = refNameProp.GetValue(shaderInput) as string ?? "";
                                }


                            }

                            // Fallback: Get the title from the node view UI
                            if (string.IsNullOrEmpty(propertyDisplayName))
                            {
                                var titleLabel = nodeView.Q<Label>("title-label");
                                if (titleLabel == null) titleLabel = nodeView.Q<Label>("title");
                                if (titleLabel == null) titleLabel = nodeView.Q<Label>();

                                if (titleLabel != null)
                                {
                                    propertyDisplayName = titleLabel.text ?? "";
                                }
                            }

                            // Also try the title property on the node view
                            if (string.IsNullOrEmpty(propertyDisplayName))
                            {
                                var titleProp = nodeView.GetType().GetProperty("title", BindingFlags.Public | BindingFlags.Instance);
                                if (titleProp != null)
                                {
                                    propertyDisplayName = titleProp.GetValue(nodeView) as string ?? "";
                                }
                            }

                            string searchText = (propertyDisplayName + " " + propertyName).ToLower();
                            
                            if (!string.IsNullOrEmpty(propertyDisplayName) && searchText.Contains(query))
                            {


                                results.Add(new NodeSearchResult
                                {
                                    NodeView = nodeView,
                                    Node = node,
                                    DisplayName = propertyDisplayName,
                                    NodeType = "Variable",
                                    IsVariable = true
                                });
                            }
                        }
                    }
                    catch (Exception e)
                    {

                    }
                }


            }
            catch (Exception e)
            {

            }

            return results;
        }



        private static void FocusOnNode(VisualElement graphView, VisualElement nodeView)
        {
            try
            {
                // For all nodes (including property nodes), use standard selection
                var clearSelectionMethod = materialGraphViewType.GetMethod("ClearSelection", BindingFlags.Public | BindingFlags.Instance);
                clearSelectionMethod?.Invoke(graphView, null);

                // Add to selection
                var addToSelectionMethod = materialGraphViewType.GetMethod("AddToSelection", BindingFlags.Public | BindingFlags.Instance);
                addToSelectionMethod?.Invoke(graphView, new object[] { nodeView });

                // Frame selection
                var frameSelectionMethod = materialGraphViewType.GetMethod("FrameSelection", BindingFlags.Public | BindingFlags.Instance);
                if (frameSelectionMethod != null)
                {
                    frameSelectionMethod.Invoke(graphView, null);
                }
                else
                {
                    // Fallback: try to scroll to the element
                    nodeView.Focus();
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"ShaderGraph Search: Could not focus on node - {e.Message}");
            }
        }

        private class NodeSearchResult
        {
            public VisualElement NodeView { get; set; }
            public object Node { get; set; }
            public string DisplayName { get; set; }
            public string NodeType { get; set; }
            public bool IsVariable { get; set; }
        }

        // Grouped results for VS Code-style navigation
        private class GroupedSearchResult
        {
            public string DisplayName { get; set; }
            public string NodeType { get; set; }
            public List<NodeSearchResult> Instances { get; set; } = new List<NodeSearchResult>();
            public int Count => Instances.Count;
        }
    }
}