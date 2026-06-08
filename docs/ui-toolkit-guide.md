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

