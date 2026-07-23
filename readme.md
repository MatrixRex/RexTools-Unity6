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
    6.  **Auto Align Inputs**: Right-click over any node with connected inputs. Select **Auto Align Inputs** to align the connected nodes horizontally, distribute them vertically with a consistent gap, and center the entire stack relative to the target node.


### ⚡ ShaderGraph Custom Function Sync
Automatically synchronizes and generates input and output ports (slots) for a `CustomFunctionNode` in the ShaderGraph editor based on the signature of its referenced HLSL file, preserving existing connections.

*   **Usage**:
    1.  Open any ShaderGraph.
    2.  Create a **Custom Function** node and set its type to **File**.
    3.  Assign your `.hlsl` source file to the node and set the **Name** of the function.
    4.  Right-click the selected Custom Function node to open the context menu.
    5.  Select **Rex Tools > Sync Ports from HLSL**.
    6.  The input and output ports will automatically populate based on the HLSL function parameters, maintaining any existing connections for matching ports.

### 👁️ ShaderGraph Output Preview
Adds a contextual menu action to preview any output slot of a selected node by connecting it directly to the Master Stack's **Emission** block (if a Lit shader) or **Base Color** block (if an Unlit shader). It automatically handles conflicting connection cleanup and updates the graph immediately.

*   **Usage**:
    1.  Open any ShaderGraph.
    2.  Select any node that has output ports.
    3.  Right-click the selected node to open the context menu.
    4.  Navigate to **Rex Tools > Preview Output** and select the specific output slot you want to preview.
    5.  The connection will automatically draw from that slot to the appropriate Master Stack input block for instant previewing.

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
A lightweight, background-threaded Git client built directly into the Unity Editor. It displays your current branch directly on the Unity play control strip and lets you stage, commit, fetch, pull, and push without leaving Unity. It supports a hierarchical **Tree View** for grouping changed files by project folders, alongside a flat **List View** option. Visual notification badges (red dots) and built-in icons highlight pending actions (modified files, unpushed commits, and remote updates to pull).

*   **Usage**:
    1.  Look at the Unity play controls bar to see the current active branch (e.g., `Git: main`).
    2.  Click the branch button or go to `Tools > Rex Tools > Git Integration` to open the full window.
    3.  See your repository path, current branch, and synchronization counts (Ahead/Behind).
    4.  The changed files list defaults to the **Tree View** tab, showing modified files grouped under their parent folders. Collapse/expand folders using their arrows.
    5.  Use the **Expand All** and **Collapse All** buttons to quickly manage the folder tree view.
    6.  Checking/unchecking folder checkboxes recursively updates all files and subfolders nested inside.
    7.  Toggle to the **List View** tab to view files in a flat list layout.
    8.  Use **Fetch**, **Pull**, and **Push** buttons to sync with remote branches.
    9.  Enter a commit message and click **COMMIT SELECTED** to stage and commit checked files, or click **DISCARD SELECTED** to discard their local changes.

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

### 📦 Scene Pack
A custom asset type that represents a grouped list of scene assets. Double-clicking a Scene Pack asset or using its custom inspector UI lets you quickly load multiple scenes into the Unity Editor at once.

*   **Usage**:
    1.  Right-click in the Project window and select `Create > Rex Tools > Scene Pack` to create a new Scene Pack asset.
    2.  Select the created asset, and configure your list of scene assets in the inspector.
    3.  Double-click the Scene Pack asset (or select it and click **OPEN SCENE PACK (REPLACE)** in its inspector) to load the first scene as Single (replacing the active scenes) and all other scenes additively.
    4.  Alternatively, click **OPEN SCENE PACK (ADDITIVE)** to load all scenes additively, keeping your currently active scenes open.

### 🎨 Palette Texture Modifier
A dedicated editor for single-color palette textures (16x16, 32x32, 64x64). Segments textures into an interactive color grid canvas, supporting cell selection, color picker editing, cell merging, cell splitting, full Undo/Redo, and PNG asset overwriting.

*   **Usage**:
    1.  Go to `Tools > Rex Tools > Palette Texture Modifier`.
    2.  Assign a target palette texture to the **PALETTE TEXTURE** slot.
    3.  Set grid dimensions or pick a preset size (**4x4**, **8x8**, **16x16**, **32x32**, or **Auto Detect**), then click **INITIALIZE GRID FROM TEXTURE**.
    4.  Click or drag-select cells on the interactive canvas (Shift-click to multi-select, Alt-click for eyedropper sampling).
    5.  Alter cell colors using the **Color Picker** or **Hex** input field.
    6.  Combine contiguous cell selections using **Merge Selected**, or restore merged cells using **Split Cell**.
    7.  Click **SAVE & OVERWRITE** to update the original PNG asset file on disk, or **Save As Copy...** to export to a new PNG file.


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
