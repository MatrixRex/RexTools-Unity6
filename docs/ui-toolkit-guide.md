# RexTools UI Toolkit Guide

Best practices for writing C# UI Toolkit (UIElements) code in RexTools. Follow these rules to avoid compilation errors and maintain layout consistency.

## 1. Style Property Pitfalls (C# `IStyle` vs. USS)
The `IStyle` interface in C# has strict limitations compared to USS (CSS). Shorthands and certain property names do not compile in C#.

### Font Styles
- **❌ DO NOT USE:** `style.unityFontStyle = FontStyle.Bold;` (CS1061 error: `IStyle` does not contain a definition for `unityFontStyle`)
- **✅ ALWAYS USE:** `style.unityFontStyleAndWeight = FontStyle.Bold;`

### Shorthands (Margin, Padding, Borders)
`IStyle` does not support shorthand properties like `margin`, `padding`, `borderWidth`, `borderColor`, or `borderRadius`. You must define individual sides/corners in C#.

- **❌ DO NOT USE:**
  ```csharp
  element.style.margin = 10;
  element.style.padding = 5;
  element.style.borderWidth = 1;
  element.style.borderColor = Color.black;
  element.style.borderRadius = 4;
  ```
- **✅ ALWAYS USE:**
  ```csharp
  // Margin
  element.style.marginTop = 10;
  element.style.marginBottom = 10;
  element.style.marginLeft = 10;
  element.style.marginRight = 10;

  // Padding
  element.style.paddingTop = 5;
  element.style.paddingBottom = 5;
  element.style.paddingLeft = 5;
  element.style.paddingRight = 5;

  // Border Widths
  element.style.borderTopWidth = 1;
  element.style.borderBottomWidth = 1;
  element.style.borderLeftWidth = 1;
  element.style.borderRightWidth = 1;

  // Border Colors
  element.style.borderTopColor = Color.black;
  element.style.borderBottomColor = Color.black;
  element.style.borderLeftColor = Color.black;
  element.style.borderRightColor = Color.black;

  // Border Radius (Corners)
  element.style.borderTopLeftRadius = 4;
  element.style.borderTopRightRadius = 4;
  element.style.borderBottomLeftRadius = 4;
  element.style.borderBottomRightRadius = 4;
  ```

## 2. USS Styling over C# Style Properties
To keep code clean and maintain design consistency, avoid setting styles programmatically in C#. Use the shared stylesheet instead:
- Always reference `RexToolsStyles.uss`.
- Add classes to elements using `element.AddToClassList("rex-box")` instead of setting borders/colors in C#.
- Toggle visibility using `element.ToggleInClassList("rex-hidden")` or `element.style.display = isVisible ? DisplayStyle.Flex : DisplayStyle.None`.

## 3. Style Property Types
In C#, UI Toolkit style properties are wrapped in `Style*` structs:
- Sizing (`width`, `height`, `fontSize`, etc.) expects `StyleLength`. Passing an integer/float compiles due to implicit conversion but is safer to assign directly:
  `element.style.fontSize = 11;`
- Alignment/Flex properties (`alignItems`, `justifyContent`, `flexDirection`) expect style enums:
  `element.style.flexDirection = FlexDirection.Row;`
  `element.style.alignItems = Align.Center;`
  `element.style.justifyContent = Justify.SpaceBetween;`

## 4. Layout & Flex Overflow Prevention (Horizontal Rows)
When building horizontal rows programmatically (e.g. `flex-direction: row` to place a label and a field next to each other), Unity's built-in fields (such as `ObjectField`, `TextField`, `EnumField`, etc.) can easily overflow the row or parent containers if not constrained.

### Preventing Overflow
- **❌ DO NOT USE:** Programmatically-created rows without explicit flex constraints:
  ```csharp
  var row = new VisualElement();
  row.style.flexDirection = FlexDirection.Row;
  
  var label = new Label("Label");
  label.style.width = 160;
  
  var texField = new ObjectField();
  texField.style.flexGrow = 1; // ⚠️ Will overflow the container when resized smaller!
  ```
- **✅ ALWAYS USE:** Apply the standard `.rex-row` class to the container row, and set `flexShrink = 0` on fixed-width labels:
  ```csharp
  var row = new VisualElement();
  row.AddToClassList("rex-row"); // Auto-applies flex-direction and handles child resizing rules

  var label = new Label("Label");
  label.style.width = 160;
  label.style.flexShrink = 0; // 💡 Prevents the label text from being squished

  var texField = new ObjectField(); // Inherits flex-grow: 1, flex-shrink: 1, min-width: 0 from .rex-row rules
  ```

## 5. Implementing the Preset System UI
RexTools features a reusable Preset save/load system using Unity's native Preset APIs. To maintain a unified aesthetic across all editor extensions, follow these steps to implement the preset UI.

### Visual Standard: Header-Aligned Icon Button
Always align the Preset button on the right-hand side of a section header, level with the section label.

#### 1. UXML Layout
Create a horizontal row with `justify-content: space-between` and `align-items: center`. Place the section header label on the left and an empty container (`preset-container-anchor` or `preset-btn-container`) on the right:
```xml
<ui:VisualElement class="rex-row" style="justify-content: space-between; align-items: center; margin-bottom: 5px;">
    <ui:Label text="SETTINGS" class="rex-section-label" style="margin-bottom: 0;" />
    <ui:VisualElement name="preset-btn-container" />
</ui:VisualElement>
```

#### 2. C# Binding
In C#, query the container and inject a compact `16x16` icon button using the reusable `RexPresetManager` module:
```csharp
var presetContainer = root.Q<VisualElement>("preset-btn-container");
if (presetContainer != null)
{
    var presetBtn = RexTools.Editor.Core.RexPresetManager.CreatePresetIconButton(settings);
    presetContainer.Add(presetBtn);
}
```
*Note: Using `CreatePresetIconButton` is preferred for sub-panels and headers. For large windows/sections, you can optionally use `CreatePresetButtons(settings)` to show full `SAVE PRESET` and `PRESETS` buttons.*

## 6. Implementing Standardized Two-Column Layouts
For dynamic list rows that display a left-side label/descriptor and a right-side input/object field, use the standardized two-column classes in `RexToolsStyles.uss`. This splits horizontal space equally (50/50) on wider windows, while enforcing distinct minimum widths when scaled down to keep labels legible and fields interactable.

### Visual Standard: Equal Split Flex Layout
- **Row Container (`.rex-row-cols-2`)**: Replaces `.rex-row`. Sets up a flex direction of `row` with `align-items: center` and ensures the row takes up 100% of the parent width.
- **Left Column (`.rex-col-left`)**: Holds the description or metadata labels (and optional checkbox toggles). It has a higher `min-width: 120px` to prevent text truncation.
- **Right Column (`.rex-col-right`)**: Holds the interactive value fields (e.g., `TextField`, `ObjectField`). It has a small `min-width: 50px` allowing it to shrink further before clipping.
- Both columns use `flex-grow: 1; flex-shrink: 1; flex-basis: 0;` so they divide extra available horizontal space equally.

### Implementation Patterns

#### Case A: Simple Label and Field
```csharp
var row = new VisualElement();
row.AddToClassList("rex-row-cols-2");

var label = new Label("Albedo Map (_BaseMap)");
label.AddToClassList("rex-col-left");

var field = new TextField();
field.AddToClassList("rex-col-right");

row.Add(label);
row.Add(field);
```

#### Case B: Multi-Element Left Column (Toggle + Label)
If you need to include a select/active checkbox alongside the label inside the left column, wrap them in a helper container inside `.rex-col-left` so the 50/50 split remains perfectly aligned across multiple rows.
```csharp
var row = new VisualElement();
row.AddToClassList("rex-row-cols-2");

// Column 1: Left Container
var leftCol = new VisualElement();
leftCol.AddToClassList("rex-col-left");
leftCol.style.flexDirection = FlexDirection.Row;
leftCol.style.alignItems = Align.Center;

var toggle = new Toggle();
toggle.style.marginRight = 4;
leftCol.Add(toggle);

var label = new Label("Normal Map (_BumpMap)");
label.style.flexGrow = 1;
label.style.flexShrink = 1;
label.style.minWidth = 0; // Allows the label text to shrink responsively within the column
leftCol.Add(label);

row.Add(leftCol);

// Column 2: Right Field
var field = new ObjectField();
field.AddToClassList("rex-col-right");
row.Add(field);
```

## 7. Standardized List Rendering System
For scrollable list containers displaying dynamically generated item rows, always use the standardized classes from `RexToolsStyles.uss`.

### Visual Standard: Row Separators and Delete States
- **List Container (`.rex-result-list`)**: Applied to a `ScrollView` element. Resets padding and aligns scroll bars.
- **Row Container (`.rex-result-item`)**: Applied to each item row. Establishes a flex direction of `row`, vertical alignment `center`, standard spacing, a height of `32px`, and a bottom border line.
- **Delete Button (`.rex-result-delete-btn`)**: A 24x24px square button that has a gray background by default and shifts to red on hover.
- **Delete Icon (`.rex-result-delete-icon`)**: A child `VisualElement` of the delete button that renders the trash/garbage bin sprite.

### Implementation Pattern (C#)
```csharp
// ScrollView container in UXML or created via C#
var scrollView = root.Q<ScrollView>("my-list");
scrollView.AddToClassList("rex-result-list");

// Add list item row
var row = new VisualElement();
row.AddToClassList("rex-result-item");

// Add optional toggle checkbox on far-left
var toggle = new Toggle { value = false };
toggle.style.marginRight = 5;
row.Add(toggle);

// Add main description label or field (using flex to take max available space)
var label = new Label("Item Name");
label.AddToClassList("rex-result-name-btn"); // or style with flex-grow: 1, flex-shrink: 1, min-width: 0
row.Add(label);

// Add standard delete button on far-right
var deleteBtn = new Button();
deleteBtn.AddToClassList("rex-result-delete-btn");

var deleteIcon = new VisualElement();
deleteIcon.AddToClassList("rex-result-delete-icon");
deleteBtn.Add(deleteIcon);

deleteBtn.clicked += () => {
    // Delete action logic
};
row.Add(deleteBtn);

scrollView.Add(row);
```

### ⚠️ Strict Developer Constraints & Protocol
To maintain design continuity across all tools in this package, all agents must adhere to the following rules:
1. **Always Use First**: You must always attempt to use the standardized list system (`.rex-result-list`, `.rex-result-item`, `.rex-result-delete-btn`, etc.) as the default for any list container.
2. **Extend, Don't Rewrite**: If the default layout doesn't fully support your tool's custom list requirements, you must extend the existing classes in `RexToolsStyles.uss` or add helper classes, rather than writing a completely separate or custom inline-styled list system.
3. **Ask for Review**: If extending the system is not technically feasible and you must deviate from the standard pattern, you **must** pause and ask the user for a design review before writing any custom list layout code.

## 8. Standardized Foldout System (RexFoldout)
For collapsible sections and groups with header labels, optional item counts, and expand/collapse icons, use the standardized `RexFoldout` component. 

### Visual Standard: Header Bar with Left Title and Right Arrow
- **Parent Wrapper (`.rex-foldout`)**: Wraps the entire component (both header and expanded content) inside a unified border box (`border-width: 1px`, `border-color: #222`, `border-radius: 4px`, `background-color`).
- **Header Bar (`.rex-foldout-header`)**: A dark hoverable horizontal bar with subtle hover highlights. A bottom border separation line separates the header from the content container only when expanded (automatically toggled via the C# code using the `.rex-foldout--collapsed` state class on the parent).
- **Title (Left)**: Bold uppercase header text (`.rex-foldout-title`).
- **Count Badge (Left)**: An optional badge formatted as `(count)` positioned directly to the right of the title (`.rex-foldout-count`).
- **Toggle Arrow (Right)**: A text-based chevron (`▼`) aligned to the far right. When collapsed, it rotates -90 degrees (`▶`) (`.rex-foldout-arrow--collapsed`).

### Implementation Pattern (C#)
`RexFoldout` extends `VisualElement` and overrides the `contentContainer` property so that any elements added to it via `.Add()` are automatically redirected to the nested collapsible panel. Additionally, it toggles the `.rex-foldout--collapsed` class list on itself to control state-based USS styles (e.g., hiding the separation border when closed).

```csharp
using RexTools.Editor.Core;

// 1. Instantiate (Params: title, count [optional], defaultExpanded [optional])
var foldout = new RexFoldout("Materials", 3, true);

// 2. Add collapse event listener (optional)
foldout.OnValueChanged += (isExpanded) => {
    Debug.Log($"Foldout state changed: {isExpanded}");
};

// 3. Add child controls (Automatically routed to internal collapsible content area)
var myLabel = new Label("Child Item");
foldout.Add(myLabel);

// 4. Add foldout to parent layout
myScrollView.Add(foldout);
```

## 9. Standardized Folder Selector (RexFolderSelector)

Use `RexFolderSelector` from `Editor/Core/RexFolderSelector.cs` (namespace `RexTools.Editor.Core`) instead of writing inline `TextField` + folder button + drag-and-drop. Provides text input, browse button, reveal-in-explorer, drag-drop, and empty-state hint.

```csharp
using RexTools.Editor.Core;

var selector = new RexFolderSelector();

// Set initial path without triggering OnValueChanged
selector.SetPathWithoutNotify("Assets/SomeFolder");

// React to path changes
selector.OnValueChanged += path => {
    Debug.Log($"Selected: {path}");
};

// Get/set the path
string current = selector.PathValue;
selector.PathValue = "Assets/NewFolder";

parent.Add(selector);
```

See `QuickShotWindow.cs` for a full usage example.

## 10. Standardized Custom Button (RexButton)
Use `RexButton` from `Editor/Core/RexButton.cs` (namespace `RexTools.Editor.Core`) for standard buttons, icon-only buttons, or toggle buttons instead of standard UI Toolkit `ui:Button`. It provides standard hovering, pressing, click flashing, toggle behavior, and supports combinations of icons and text.

### Constructor
```csharp
public RexButton(string label = null, Texture2D icon = null, bool isToggle = false, bool defaultActive = false)
```

### Properties & Events
- **`Label`**: Get or set the text label.
- **`Icon`**: Get or set the button's icon texture.
- **`IsToggle`**: Set to `true` to enable toggle button behavior.
- **`IsActive`**: Get or set the active state of the toggle (toggles `.rex-button--active` class).
- **`OnClick`**: Fired when the button is clicked.
- **`OnToggleChanged`**: Fired when the active toggle state changes.

### Methods
- **`SetActiveWithoutNotify(bool active)`**: Sets the toggle state without triggering `OnToggleChanged`.

### Implementation Pattern (C#)
```csharp
using RexTools.Editor.Core;

// Standard button
var standardBtn = new RexButton("Click Me");
standardBtn.OnClick += () => Debug.Log("Clicked!");

// Icon-only button
Texture2D refreshIcon = EditorGUIUtility.IconContent("d_Refresh").image as Texture2D;
var iconBtn = new RexButton(icon: refreshIcon);

// Toggle button
var toggleBtn = new RexButton("Auto-Save: OFF", isToggle: true, defaultActive: false);
toggleBtn.OnToggleChanged += active => {
    toggleBtn.Label = $"Auto-Save: {(active ? "ON" : "OFF")}";
};
```

## 11. Standardized Action Button (RexActionButton)
Use `RexActionButton` from `Editor/Core/RexActionButton.cs` (namespace `RexTools.Editor.Core`) for prominent primary action buttons (e.g. process/execution buttons at the bottom of a window). It inherits from `VisualElement`, styles itself using the `.rex-action-button` class, handles disabled states, and implements dynamic hovering/pressing background color tints in C#.

### Constructor
```csharp
public RexActionButton(string label = null, Texture2D icon = null, Color? tint = null)
```
*Note: The default tint is `#3380FF` (`new Color(0.2f, 0.5f, 1f)`), which matches the standard blue highlight color.*

### Properties & Events
- **`Label`**: Get or set the text label.
- **`Icon`**: Get or set the button's icon texture.
- **`Tint`**: Get or set the primary background tint color.
- **`IsEnabled`**: Get or set the interactive state. Setting this to `false` automatically dims the button (opacity: 0.4) and disables click events.
- **`OnClick`**: Fired when the button is clicked (if `IsEnabled` is true).

### Methods
- **`SetEnabledWithoutNotify(bool enabled)`**: Sets the enabled state and updates the visual appearance without notifying any subscribers.

### Implementation Pattern (C#)
```csharp
using RexTools.Editor.Core;

// Standard blue action button
var actionBtn = new RexActionButton("PROCESS ASSETS");
actionBtn.OnClick += () => StartProcess();

// Custom colored action button (e.g., Red for danger/destructive actions)
var dangerBtn = new RexActionButton("DELETE ALL", tint: new Color(0.8f, 0.2f, 0.2f));

// Setting enabled/disabled state
actionBtn.IsEnabled = false; // Automatically dims the button to 40% opacity and blocks clicks
```

## 12. Standardized Window Layout Structure (ScrollView Wrapping)
To prevent window contents from clipping or overflowing on smaller screens, always wrap the central content area in a `ScrollView` and keep headers and actions fixed.

### Layout Guidelines
1. **Header (Fixed)**: Placed directly on the window's root. It contains the branded title and help button.
2. **Help Box (Fixed)**: Placed directly on the window's root below the header. It toggles visibility but does not scroll.
3. **ScrollView (Scrollable)**: A vertical scroll area that handles all central content. It must fill the remaining vertical space using flex-grow.
4. **Primary Actions (Fixed, Optional)**: Execution buttons (e.g., `RexActionButton`) are placed at the bottom of the root window, outside the ScrollView, so they remain fixed and always visible.

### Implementation Pattern (C#)
```csharp
public void CreateGUI()
{
    VisualElement root = rootVisualElement;
    root.AddToClassList("rex-root-padding");
    // (Load stylesheet and build header/helpBox)

    root.Add(header);
    root.Add(helpBox);

    // --- SCROLLABLE CONTENT AREA ---
    var scrollView = new ScrollView(ScrollViewMode.Vertical);
    scrollView.style.flexGrow = 1;
    scrollView.style.marginTop = 4;
    scrollView.style.marginBottom = 4;
    root.Add(scrollView);

    // --- CENTRAL CONTENT (Added to scrollView) ---
    var contentBox = new VisualElement();
    contentBox.AddToClassList("rex-box");
    scrollView.Add(contentBox);

    // --- MAIN ACTIONS (Fixed at bottom of root) ---
    var actionBtn = new RexActionButton("EXECUTE");
    root.Add(actionBtn);
}
```




