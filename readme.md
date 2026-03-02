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
    
### 📸 Quick Shot
A high-resolution screenshot utility that allows you to capture the Scene view or Game view with custom scaling and transparency.

*   **Usage**:
    1.  Go to `Tools > Rex Tools > Quick Shot`.
    2.  Set the **EXPORT PATH** where you want to save your screenshots.
    3.  Choose between **Scene** or **Game** mode.
    4.  (Game Mode Only) Adjust **Render Scale** for higher resolution or toggle **Transparent BG**.
    5.  Click **CAPTURE SCREENSHOT**.

### 🧹 Unused Assets Finder
Scans your project for assets that are not referenced in any scene or by other assets, helping you identify and remove redundant files to keep your project clean.

*   **Usage**:
    1.  Go to `Tools > Rex Tools > Unused Assets Finder`.
    2.  Set the **Folder** path (default is `Assets`).
    3.  Toggle **Recursive Search** to scan subdirectories.
    4.  Click **FIND UNUSED ASSETS**.
    5.  Browse results categorized by **Textures**, **Prefabs**, **Models**, and **Other**.
    6.  Click an asset name to ping it in the project, or use the **Trash Icon** to delete it.
    


### 🖼️ Texture Repacker
A utility for packing multiple textures into individual RGBA channels or extracting existing channels into separate images.

*   **Usage**:
    1.  Go to `Tools > Rex Tools > Texture Repacker`.
    2.  **PACK**: Drag up to 4 textures into the RGBA slots. Swizzle channels using the (R|G|B|A) buttons, or use the **VAL** override for constant values.
    3.  **UNPACK**: Drag a source texture to extract its channels. Choose which channels to extract and set custom suffixes.
    4.  Set the **Output Name** and **Path**, then click **PACK** or **UNPACK**.

### 📐 ShaderGraph Organizer
Adds alignment and distribution tools to the ShaderGraph editor context menu. Quickly organize selected nodes to clean up your graph layout.

*   **Usage**:
    1.  Open any ShaderGraph.
    2.  Select 2 or more nodes in the graph.
    3.  Right-click to open the context menu.
    4.  **Align**: Choose **Left**, **Right**, **Up**, or **Down** to snap nodes to a common edge.
    5.  **Distribute**: Choose **Horizontal** or **Vertical** to evenly space nodes with a consistent gap.



---

## 🚀 Getting Started
To install RexTools in your Unity project:

1. Open the **Unity Package Manager** (`Window > Package Manager`).
2. Click the **+** button in the top-left corner.
3. Select **Add package from git URL...**.
4. Paste the following URL:
   `https://github.com/MatrixRex/RexTools-Unity6.git`
5. Click **Add**.

## 📜 License
MIT
