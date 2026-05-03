# RexTools Design Guidelines

All UI tools and Editor extensions within RexTools should adhere to these visual and structural guidelines to maintain a cohesive, professional look.

## Core Principles
- **UI Toolkit Preferred**: All new tools should be built using Unity's UI Toolkit (UXML/USS) rather than IMGUI whenever possible.
- **Shared Stylesheet**: Always link and utilize `RexToolsStyles.uss` to apply standard styles.

## Visual Elements & Classes

### 1. Root & Typography
- **Root Padding (`.rex-root-padding`)**: Add 12px padding to the main root container.
- **Header (`.rex-header`)**: Use for main tool titles. Bold, 16px, color `#66CCFF`.
- **Tool Title (`.rex-tool-title`)**: Standard title, bold, 14px, color `#CCC`.

### 2. Containers
- **Boxes (`.rex-box`)**: Group related controls inside these dark, translucent containers (`rgba(51, 51, 51, 0.4)` background, `#222` border, 4px border radius).
- **Section Labels (`.rex-section-label`)**: Use inside `.rex-box` for grouping titles. Bold, 10px, uppercase, color `#888`.
- **Rows (`.rex-row`)**: For horizontally aligned items. It automatically applies `align-items: center` and margin spacing.

### 3. Buttons & Actions
- **Primary Action Buttons (`.rex-action-button`)**: Use for the main execution task at the bottom of a tool. Height 50px, bold 14px text.
  - Modifier `.rex-action-button--pack`: Blue highlight background (`#3380FF`).
  - Modifier `.rex-action-button--unpack`: Green highlight background (`#33B373`).
- **Icon Buttons (`.rex-icon-button`)**: 24x24 square buttons with `#282828` background, turning blue (`#4D99FF`) on hover. Use inside lists or for small actions (add/remove/help).
- **Small Selectors (`.rex-button-small`)**: For toggleable options or compact buttons. Turns blue when active (`.rex-button-small--active`).

### 4. Interactive Fields & States
- **Drag & Drop Field (`.rex-drag-drop-field`)**: For custom drag-and-drop areas. Uses a `#1a1a1a` border and dark background. Modifier `.rex-drag-drop-field--active` turns the border blue.
- **Hidden Elements (`.rex-hidden`)**: Use to quickly toggle visibility via `display: none`.

### 5. Help Integration
- **Help Button (`.rex-help-btn`)**: Placed in the `.rex-header-row`, usually aligned to the right. Use the modifier `.rex-help-btn--active` when toggled on to highlight it.
- **Help Box (`.rex-help-box`)**: A designated container for instructions. It should generally have `.rex-box` and start with `.rex-hidden`. Toggle `.rex-hidden` via script when the help button is pressed. Use `.rex-help-text-title` and `.rex-help-text-item` for text inside it.

## General Layout Structure (Example)
```xml
<ui:UXML xmlns:ui="UnityEngine.UIElements" xmlns:uie="UnityEditor.UIElements">
    <ui:VisualElement class="rex-root-padding">
        <!-- Header -->
        <ui:VisualElement class="rex-header-row">
            <ui:VisualElement class="rex-header-stack">
                <ui:Label text="REXTOOLS" class="rex-brand-label" />
                <ui:Label text="Tool Name" class="rex-tool-title" />
            </ui:VisualElement>
            <ui:Button name="help-btn" class="rex-help-btn" />
        </ui:VisualElement>

        <!-- Help Box -->
        <ui:VisualElement name="help-box" class="rex-help-box rex-box rex-hidden">
            <ui:Label text="HOW TO USE:" class="rex-help-text-title" />
            <ui:Label text="• Follow these steps..." class="rex-help-text-item" />
        </ui:VisualElement>

        <!-- Content Section -->
        <ui:VisualElement class="rex-box">
            <ui:Label text="SECTION TITLE" class="rex-section-label" />
            <ui:VisualElement class="rex-row">
                <!-- Controls go here -->
            </ui:VisualElement>
        </ui:VisualElement>

        <!-- Primary Action -->
        <ui:Button text="EXECUTE" class="rex-action-button rex-action-button--pack" />
    </ui:VisualElement>
</ui:UXML>
```
