# Batch Material Processor Design Specification

Design document for the **Batch Material Processor**, a Unity editor extension within the RexTools package.

## 1. Goal & Requirements
The Batch Material Processor allows artists to bulk-assign texture maps to multiple materials. It supports a two-step process: scanning matches first, then applying changes.

### Core Features:
- **Material List**: Populate from active selection (scene objects/renderers or asset files) or manual drag-and-drop.
- **Shader Application**: Set a target shader that is applied to all materials before texture mapping.
- **Texture Directory**: Specify a search folder (with recursive toggle) where matching textures reside.
- **Custom Suffix Mapping**: Define which filename suffixes (e.g. `_albedo`, `_basecolor`) correspond to which shader texture property (e.g. `_BaseMap`).
- **Dry-run Match Preview**: A "Process" step to preview matches before modifying assets.
- **Apply Changes**: Confirm and batch-write shaders and texture assignments.

---

## 2. Architecture & Directory Structure
All files will reside under:
`Editor/Batch Material Processor/`

### File Layout:
- `BatchMaterialProcessorSettings.cs`: ScriptableObject settings containing lists of materials, target shader, search path, and suffix mappings.
- `BatchMaterialProcessorWindow.cs`: The EditorWindow implementation containing the UI bindings.
- `BatchMaterialProcessorWindow.uxml`: The UI Toolkit structure.
- `BatchMaterialProcessorTypes.cs`: Data containers for preview rows and suffix lists.

---

## 3. Data Persistence & Preset Support
Settings are stored in an in-memory `BatchMaterialProcessorSettings` ScriptableObject.
- Integrates with `RexPresetManager` to save/load configurations as standard Unity `.preset` files (e.g. "URP_Default.preset").
- Persists user preferences for materials and paths across session compiles.

---

## 4. Texture Suffix Matching Algorithm
We match textures by checking if the texture filename contains both a normalized representation of the material name and a valid suffix.

### Steps:
1. **Normalize Strings**: Remove spaces, underscores, hyphens, and convert to lowercase.
   - e.g., `Wood_Oak_01` -> `woodoak01`
   - e.g., `_BaseColor` -> `basecolor`
2. **Scan Directory**: Get all textures in the selected folder.
3. **Score Matches**:
   - **Perfect Match (Score 3)**: Normalized texture name starts with normalized material name and ends exactly with normalized suffix.
   - **Strong Match (Score 2)**: Normalized texture name contains normalized material name and ends with normalized suffix.
   - **Weak Match (Score 1)**: Normalized texture name contains both normalized material name and normalized suffix.
4. **Tie Breaking**: Select the file with the highest score. If scores are equal, select the shortest filename.

---

## 5. UI Layout (Two-Panel Design)
The interface is built with UI Toolkit using `.rex-*` styles:

- **Left Column: Configuration**:
  - Material list (add/remove items, get from selection).
  - Target Shader picker.
  - Folder search picker (with drag-and-drop support).
- **Right Column: Tabs**:
  - **Tab 1 (Suffix Mapping)**: Grid mapping each texture property to a text field containing comma-separated suffixes.
  - **Tab 2 (Match Preview)**: List of materials. Expanding a material displays all texture fields, the matched texture, and an override picker.
- **Footer**:
  - "PROCESS MATCHES" (Blue) to perform the dry run.
  - "APPLY" (Green) to write changes to material assets.
