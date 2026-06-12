# 2026-06-12 Texture Preview Style Design

This specification details the updates to the `RexTexturePreview` component to add a border, rounded corners, drop-component background styling, and a dynamic placeholder message when no texture is bound.

## Requirements

1. **Aesthetics**:
   - Add a 1px border (`#1a1a1a`) and rounded corners (`4px`) matching the drop zones (`RexTextureField`).
   - Use the drop component background color (`rgba(30, 30, 30, 0.9)`) instead of `Color.black` when no preview is showing.
   - Clip preview textures within the rounded corners (`overflow: hidden`).
2. **Dynamic Messages**:
   - Display context-specific placeholder labels when no texture is present.
   - Text variants:
     - Pack tab: `"packed texture preview"`
     - Mix tab: `"mixed texture preview"`
     - Unpack tab: `"channel preview"`
3. **Behavior**:
   - Hide the placeholder message and show the maximize button once a texture is bound.
   - Show the placeholder message and hide the maximize button when no texture is bound.

## Proposed Changes

### 1. USS Styles (`Editor/RexToolsStyles.uss`)

Add classes for the preview component:
```css
/* RexTexturePreview Component Styles */
.rex-texture-preview {
    background-color: rgba(30, 30, 30, 0.9);
    border-width: 1px;
    border-color: #1a1a1a;
    border-radius: 4px;
    overflow: hidden;
}

.rex-texture-preview__image {
    width: 100%;
    height: 100%;
    background-color: transparent;
}

.rex-texture-preview__placeholder {
    position: absolute;
    left: 0;
    top: 0;
    right: 0;
    bottom: 0;
    -unity-text-align: middle-center;
    color: #666666;
    font-size: 10px;
    white-space: normal;
    padding: 8px;
    pointer-events: none;
}
```

### 2. Texture Preview Class (`Editor/Core/RexTexturePreview.cs`)

- Add `placeholderLabel` variable.
- Update constructor to accept `string placeholderText = "No Preview"`.
- Remove inline `backgroundColor = Color.black` and use `.AddToClassList()`.
- Add stylesheet class calls.
- Implement an `UpdateVisibility()` helper to toggle display of `placeholderLabel` and `maxBtn`.
- Trigger `UpdateVisibility()` inside the constructor and the `image` setter.

### 3. Editor Window calls (`Editor/Texture Repacker/TexturePackSeparator.cs`)

Pass custom placeholders:
- Pack Preview: `new RexTexturePreview(160, "Show full-size preview", "packed texture preview")`
- Mix Preview: `new RexTexturePreview(160, "Show full-size preview", "mixed texture preview")`
- Unpack Previews: `new RexTexturePreview(90, $"Show full-size {names[idx]} preview", "channel preview")`

## Verification Plan

### Manual Verification
- Open the Texture Repacker editor window (`Tools/Rex Tools/Texture Repacker`).
- Verify that each tab's preview displays the correct custom text, border, and background color when empty.
- Drag-drop textures into the slots to verify that the placeholder text hides and the preview image/maximize button display properly inside the rounded corners.
