# 2026-06-12 Texture Field Clear Button Design

This specification details the addition of a "clear" button to the `RexTextureField` component.

## Requirements

1. **Aesthetics**:
   - Add a small 20x20px clear button (`.rex-field-clear-btn`) inside the `RexTextureField` container.
   - Position it in the upper-right corner (`position: absolute; right: 4px; top: 4px;`).
   - Use the existing `remove.png` icon (`.rex-icon-remove`) centered inside the button.
   - Style the button with a semi-transparent black background by default (`rgba(0, 0, 0, 0.4)`) and a red background on hover (`#B33333`) to indicate destructive action.
2. **Behavior & Visibility**:
   - The button should only be visible when there is an active texture assigned (`currentTexture != null`).
   - Do not display the button when in value/custom color mode (`previewImage.style.backgroundColor != Color.clear`).
   - Clicking the button should clear the assigned texture (`SetTexture(null, true)`).
3. **Event Propagation**:
   - Crucial: Stop propagation of `MouseDownEvent` and `ClickEvent` on the clear button to prevent triggering the `RexTextureField` click callback (which opens the Unity object picker dialog).

## Proposed Changes

### 1. USS Styles (`Editor/RexToolsStyles.uss`)

Add classes for the clear button:
```css
/* RexTextureField Clear Button Styles */
.rex-field-clear-btn {
    position: absolute;
    right: 4px;
    top: 4px;
    width: 20px;
    height: 20px;
    background-color: rgba(0, 0, 0, 0.4);
    border-radius: 3px;
    border-width: 0;
    justify-content: center;
    align-items: center;
    padding: 0;
    cursor: link;
}

.rex-field-clear-btn:hover {
    background-color: #B33333;
}

.rex-field-clear-btn:hover .rex-icon-remove {
    -unity-background-image-tint-color: white;
}
```

Make sure `.rex-drag-drop-field` is styled as `position: relative;` to support absolute positioning of its child:
```css
.rex-drag-drop-field {
    position: relative;
    /* ... existing styles ... */
}
```

### 2. Texture Field Class (`Editor/Core/RexTextureField.cs`)

- Declare a `private Button clearBtn` field.
- Instantiate `clearBtn` in the constructor.
- Add event listeners to `clearBtn` to:
  1. Clear the texture on click: `SetTexture(null, true)`.
  2. Prevent event propagation (`MouseDownEvent`, `ClickEvent`) to avoid popping up the object picker.
- Add a helper `UpdateClearButtonVisibility()` to show/hide the button.
- Call `UpdateClearButtonVisibility()` inside `SetTexture()`, `SetColor()`, and `ClearColor()`.

## Verification Plan

### Manual Verification
- Open the Texture Repacker editor window (`Tools/Rex Tools/Texture Repacker`).
- Ensure no clear button is displayed when slots are empty.
- Drag-drop a texture into a PACK slot or click to pick a texture. Verify that the clear button appears in the upper-right corner of the slot.
- Click the clear button. Verify that the texture is cleared, the placeholder text resets to default, and the Unity object selector does **not** open.
- Toggle a PACK slot to "Value" mode. Verify that the clear button is hidden while the custom color preview is visible.
- Repeat verification for the UNPACK source slot and MIX slot.
