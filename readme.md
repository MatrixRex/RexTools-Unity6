# RexTools

A collection of professional utility tools for Unity 6.

## 🛠️ Tools

### 🔍 ShaderGraph Search
Adds a powerful search bar to the ShaderGraph editor, inspired by IDE-style navigation. It allows you to quickly find and jump to any node, group, or blackboard variable within your graph.

*   **Usage**: 
    1.  Open any ShaderGraph.
    2.  Use the search bar located in the top-right of the toolbar.
    3.  Type a node name or type (e.g., "Add", "Texture", "Variable").
    4.  Select a result from the dropdown to frame it instantly.
    5.  Use the **Up/Down** buttons to cycle through multiple instances of the same node type.


### 🖼️ Texture Repacker
A utility for packing multiple textures into individual RGBA channels or extracting existing channels into separate images.

*   **Usage**:
    1.  Go to `Tools > Texture Repacker`.
    2.  **PACK**: Drag up to 4 textures into the RGBA slots. Swizzle channels using the (R|G|B|A) buttons, or use the **VAL** override for constant values.
    3.  **UNPACK**: Drag a source texture to extract its channels. Choose which channels to extract and set custom suffixes.
    4.  Set the **Output Name** and **Path**, then click **PACK** or **UNPACK**.



---

## 🚀 Getting Started
*Installation and setup instructions.*

## 📜 License
MIT
