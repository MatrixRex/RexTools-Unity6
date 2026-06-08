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

### 🔗 ShaderGraph Node Navigator
A connection-aware navigation system for ShaderGraph. It allows you to traverse through node inputs and outputs connections.

*   **Usage**:
    1.  Select any node in the ShaderGraph.
    2.  Use the **Back/Forward** buttons in the toolbar to navigate through connected nodes.
    3.  Use the **Next/Previous Connection** (Up/Down) buttons to cycle through multiple nodes connected to the same port.
    4.  The branch selector shows your current position in the connection list (e.g., `(2/5)`).

### 📐 ShaderGraph Organizer
Adds alignment and distribution tools to the ShaderGraph editor context menu. Quickly organize selected nodes to clean up your graph layout.

*   **Usage**:
    1.  Open any ShaderGraph.
    2.  Select 2 or more nodes in the graph.
    3.  Right-click to open the context menu.
    4.  **Align**: Choose **Left**, **Right**, **Up**, or **Down** to snap nodes to a common edge.
    5.  **Distribute**: Choose **Horizontal** or **Vertical** to evenly space nodes with a consistent gap.

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
    2.  **PACK**: Drag up to 4 textures into the RGBA slots. Swizzle channels using the (R\|G\|B\|A) buttons, or use the **VAL** override for constant values.
    3.  **UNPACK**: Drag a source texture to extract its channels. Choose which channels to extract and set custom suffixes.
    4.  **MIX**: Combine a **Base** and **Layer** texture using blend modes (Multiply, Overlay, Soft Light, etc.). Customize channels and opacity for each layer.
    5.  **PREVIEW**: Use the **⛶ Maximize** button on any tab to open a high-resolution, original-size preview window for detailed inspection.
    6.  Set the **Output Name** and **Path**, then click the action button to save your PNG.




### 🐙 Git Integration
A lightweight, background-threaded Git client built directly into the Unity Editor. It displays your current branch directly on the Unity play control strip and lets you stage, commit, fetch, pull, and push without leaving Unity.

*   **Usage**:
    1.  Look at the Unity play controls bar to see the current active branch (e.g., `Git: main`).
    2.  Click the branch button or go to `Tools > Rex Tools > Git Integration` to open the full window.
    3.  See your repository path, current branch, and synchronization counts (Ahead/Behind).
    4.  Use **Fetch**, **Pull**, and **Push** buttons to sync with remote branches.
    5.  Enter a commit message and click **STAGE & COMMIT ALL** to commit local modifications.
    6.  The console log at the bottom displays live output from background Git processes.

### 🎨 Batch Material Processor
A utility for bulk-assigning texture maps to multiple materials at once. It automatically finds texture files inside a directory using custom suffix matching, runs a dry-run preview, and applies the target shader and matched textures in one click.

*   **Usage**:
    1.  Go to `Tools > Rex Tools > Batch Material Processor`.
    2.  Use **GET FROM SELECTION** to populate the material list from selected GameObjects in the scene or selected Material assets, or drag-and-drop them directly into the list.
    3.  Select the target **Shader** to apply (this dynamically populates the available texture properties).
    4.  Set the texture search **Folder** path (drag-and-drop is supported) and choose whether to search recursively.
    5.  Go to the **Suffixes** tab to review or edit suffix matching patterns (e.g. Albedo -> `_albedo, _basecolor`).
    6.  Click **PROCESS MATCHES** to perform a dry run. The window will switch to the **Preview** tab, showing matched texture paths for each material property.
    7.  Toggle off individual properties/materials to exclude them, or select a custom override texture from the asset picker.
    8.  Click **APPLY** to set the target shader and textures on all materials.

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
