# 2026-06-12 Texture Pack Invert Button & Container Design

This specification details updates to the PACK tab on the `TexturePackSeparator` editor window:
1. Replacing the standard Unity `Toggle` for channel inverting with a custom toggle `RexButton` matching the UNPACK tab.
2. Wrapping the channel grid inside a container (`rex-box`) with the title `"PACK SETTINGS"`.

## Requirements

1. **Invert Button Consistency**:
   - Replace the standard Unity `Toggle` with a `RexButton` styled exactly like the "Invert Channel" button in the UNPACK slots.
   - Use the same text: `"Invert Channel"`.
   - Apply the same layout constraints (`flexGrow = 1`, `height = 18`, `fontSize = 9`).
   - Listen to `OnToggleChanged` to update `packSlots[index].invert` and call `UpdatePreview()`.
2. **Layout Packaging**:
   - Create a `channelsBox` container with the `rex-box` CSS class.
   - Add a header label: `"PACK SETTINGS"` (styled bold, grey, size 10, margin 5).
   - Place the channel grid (`rex-grid`) inside this `channelsBox`.
   - Add `channelsBox` to `packContainer`.

## Proposed Changes

### 1. Editor Window Updates (`Editor/Texture Repacker/TexturePackSeparator.cs`)

#### Invert Toggle Replacement (around lines 327–330)
Replace the old `Toggle` with a `RexButton` row setup:
```csharp
                // Row for Invert button
                var invertRow = new VisualElement();
                invertRow.AddToClassList("rex-row");
                invertRow.style.justifyContent = Justify.Center;
                invertRow.style.marginTop = 6;
                invertRow.style.marginBottom = 2;

                var invertBtn = new RexButton("Invert Channel", isToggle: true, defaultActive: packSlots[index].invert);
                invertBtn.style.flexGrow = 1;
                invertBtn.style.height = 18;
                invertBtn.style.fontSize = 9;
                invertBtn.OnToggleChanged += active => {
                    packSlots[index].invert = active;
                    UpdatePreview();
                };
                invertRow.Add(invertBtn);
                textureContainer.Add(invertRow);
```

#### Channels Grid Container (around lines 265–374)
Wrap the `grid` generation inside a new `channelsBox` element:
```csharp
            // --- CHANNEL SLOTS GRID ---
            var channelsBox = new VisualElement { style = { flexShrink = 0 } };
            channelsBox.AddToClassList("rex-box");
            channelsBox.Add(new Label("PACK SETTINGS") { style = { unityFontStyleAndWeight = FontStyle.Bold, fontSize = 10, marginBottom = 5, color = Color.gray } });

            var grid = new VisualElement { style = { flexDirection = FlexDirection.Row, flexWrap = Wrap.Wrap, justifyContent = Justify.SpaceBetween, flexShrink = 0 } };
            grid.AddToClassList("rex-grid");
            
            // ... grid generation and card additions ...

            channelsBox.Add(grid);
            packContainer.Add(channelsBox);
```

## Verification Plan

### Manual Verification
- Open the Texture Repacker editor window (`Tools/Rex Tools/Texture Repacker`).
- Select the PACK tab.
- Verify that the four channel boxes are nested inside a single container titled `"PACK SETTINGS"`.
- Verify that each of the four slot cards contains a full-width `"Invert Channel"` button.
- Bind a texture, click the `"Invert Channel"` button, and verify that the preview updates to show the inverted channel values.
