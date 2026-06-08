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
