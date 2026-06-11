# Texture Repacker UI Refactoring Design Document

**Date**: 2026-06-11
**Topic**: Refactoring the Texture Repacker tool's controls and layout to reuse existing core components and eliminate inline styling.

## 1. Goal
Improve UI consistency and design aesthetics of the Texture Repacker tool (`TexturePackSeparator.cs`) within the RexTools UPM package by:
- Replacing custom/basic UI sliders and float fields with the standardized `RexSlider` component.
- Replacing the main processing button with the custom `RexActionButton` with tab-specific background tints.
- Cleaning up inline C# style properties in favor of the shared stylesheet (`RexToolsStyles.uss`) and layout classes (`.rex-row`, `.rex-row-cols-2`, `.rex-col-left`, `.rex-col-right`).

## 2. Component Reuses

### RexActionButton
- **Target**: Bottom window action/footer button `actionButton`.
- **Changes**:
  - Class type changed from `UnityEngine.UIElements.Button` to `RexTools.Editor.Core.RexActionButton`.
  - Subscribed event changed from `clicked` to `OnClick`.
  - Background color tint dynamically updated on tab change inside `SwitchTab(int index)`:
    - **PACK (Tab 0)**: Emerald Green (`new Color(0.2f, 0.6f, 0.3f)`)
    - **UNPACK (Tab 1)**: Orange-Red (`new Color(0.7f, 0.3f, 0.2f)`)
    - **MIX (Tab 2)**: Royal Blue (`new Color(0.2f, 0.5f, 0.8f)`)

### RexSlider
- **Target A**: Custom value sliders inside the 4 channel slots in the PACK tab.
  - **Changes**: Replaced inline `Slider` + `FloatField` layout with a single `RexSlider(0f, 1f, defaultValue: 0.5f, value: packSlots[index].customValue)`.
  - **Syncing**: Keep track of slots' sliders using a list (`private List<RexSlider> slotSliders`) to update their enabled state inside `UpdateButtonStates()`.
- **Target B**: Opacity slider in the MIX tab.
  - **Changes**: Replaced the custom dual-sync `Slider` + `FloatField` with `RexSlider(0f, 1f, defaultValue: 1f, value: mixOpacity)`.

## 3. Layout and USS Enhancements
- **Two-Column Setup (`.rex-row-cols-2`)**: Name and path output rows in PACK, UNPACK, and MIX tabs refactored to use standard flex columns `.rex-col-left` (labels) and `.rex-col-right` (text fields/folder selectors), eliminating inline widths.
- **Horizontal Rows (`.rex-row`)**: General row containers upgraded to use `.rex-row` instead of inline `FlexDirection.Row` and alignment.

## 4. Verification Plan
- Verify compilation within the `RexTools.Editor` assembly.
- Verify visual styling and tab switches in Unity.
- Verify sliders (custom values, opacity) and ensure values compile and encode correctly when PACK, UNPACK, and MIX are executed.
