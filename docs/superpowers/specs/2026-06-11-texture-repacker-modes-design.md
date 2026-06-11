# Texture Repacker Channel Modes UI Design Document

**Date**: 2026-06-11
**Topic**: Refactoring the PACK tab channel slots inside the Texture Repacker tool (`TexturePackSeparator.cs`) to feature two distinct, clean UI modes per slot: Texture Mode and Constant Value Mode.

## 1. Goal
Make the PACK tab workflow easier to understand by showing only relevant settings based on the active mode of each slot, eliminating visual clutter.

## 2. UI Layout per Slot
Each channel slot (RED, GREEN, BLUE, ALPHA) will be housed in a `.rex-box` container with the following hierarchy:
1. **Slot Header**: Label displaying the slot name (e.g. "RED (R)") with its channel color tint.
2. **Mode Selector Row**: A two-button row using `RexButton`:
   - **Texture**: Activates Texture Channel Mode.
   - **Value**: Activates Constant Value Mode.
3. **Sub-Containers**:
   - **Texture Container (`textureContainer`)**:
     - Active when Texture Mode is selected.
     - Houses the `RexTextureField` drop zone.
     - Houses the channel swizzle buttons row (`R`, `G`, `B`, `A`).
     - Houses the `Invert` toggle.
   - **Value Container (`valueContainer`)**:
     - Active when Value Mode is selected.
     - Houses the `RexSlider` (0.0 to 1.0 range, default 0.5).

## 3. State Management & Syncing
- When **Texture Mode** is selected:
  - `packSlots[index].useCustom` is set to `false`.
  - `textureContainer` is shown (`DisplayStyle.Flex`), `valueContainer` is hidden (`DisplayStyle.None`).
- When **Constant Value Mode** is selected:
  - `packSlots[index].useCustom` is set to `true`.
  - `textureContainer` is hidden (`DisplayStyle.None`), `valueContainer` is shown (`DisplayStyle.Flex`).
- **UpdateButtonStates()**:
  - Updates the active status (`IsActive = true/false`) of the Mode buttons (`Texture` and `Value` buttons).
  - Updates visibility of `textureContainer` and `valueContainer` for each slot.
  - Updates the active states of swizzle buttons (`R`, `G`, `B`, `A`) and custom color previewing on slot drop zones.

## 4. Verification Plan
- Verify window compiles and opens.
- Verify that toggling between "Texture" and "Value" buttons hides/shows the appropriate sections.
- Verify that changing values or textures updates the Combined Preview dynamically.
