# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added
- Palette Texture Modifier: New tool for editing low-resolution palette textures (16x16, 32x32, 64x64) with interactive grid canvas (`PaletteCanvasElement`), custom cell segmentation, live Color Picker and Hex editing, cell merging and splitting, full Undo/Redo integration, and PNG asset file overwriting.

### Optimized
- ShaderGraph Tools: Gated background `OnEditorUpdate` execution in `ShaderGraphSearchExtension`, `ShaderGraphOutputPreviewExtension`, `ShaderGraphOrganizerExtension`, and `ShaderGraphCustomFunctionSyncExtension` to only run when a Shader Graph window is focused (`EditorWindow.focusedWindow`), eliminating per-frame `Resources.FindObjectsOfTypeAll` heap scans and GC overhead when working elsewhere in Unity Editor.


## [0.6.0] - 2026-07-14

### Added
- ShaderGraph Organizer: Added "Auto Align Inputs" context menu action when right-clicking a node, which aligns connected input nodes horizontally, distributes them vertically, centers the stack relative to the target node, and updates the graph selection.
- ShaderGraph Output Preview: Added a context menu option to preview any output slot of a selected node by connecting it directly to the master stack's Base Color (Unlit) or Emission (Lit) input blocks, with automatic validation, conflicting connection cleanup, full undo support, and a `Restore Connection` action to revert the preview and restore original inputs.
- ShaderGraph Custom Function Sync: Added a context menu action `Rex Tools/Sync Ports from HLSL` to automatically synchronize and generate input/output slots for `CustomFunctionNode` based on an HLSL file signature, preserving existing connections.
- Git Integration: Hierarchical collapsible folder tree view as the default view, and list view on a separate tab (using `RexTabGroup`).
- Git Integration: Bulk directory operations where checking/unchecking a folder checkbox recursively updates all files/folders nested inside it.
- Git Integration: Added 'Expand All' and 'Collapse All' buttons to quickly expand or collapse all directory nodes in the tree view.
- Git Integration: Optimized UI rebuilding and tab switching performance using a dual ScrollView layout (`treeViewScroll` and `listViewScroll`), in-place checkbox syncing, in-place folder expand/collapse updates, and a local icon cache (`iconCache`) to bypass slow disk checks and `AssetDatabase` queries.
- Git Integration: Added an animated loading state showing a spinner directly in the changed files list and status bar when refreshing Git status, preventing the appearance of a frozen UI.
- Git Integration: Added visual notification badges (red dots) on the Changes and History tabs to indicate local modifications and unpushed/unpulled commits.
- Git Integration: Upgraded Fetch, Pull, and Push action buttons to feature built-in Unity version control icons and styled them matching the rex-button design system.
- Git Integration: Added notification badges (dots) over the Pull and Push buttons that automatically appear when there are incoming changes (behind > 0) or outgoing changes (ahead > 0).
- Scene Pack: A custom asset type that represents a list of SceneAssets with custom icon support, allowing users to open all configured scenes at once by double-clicking the asset or using the 'Open Scene Pack' buttons in its custom inspector UI.
- Core: Added a "clear" button component to `RexTextureField` slots that appears when a texture is bound, enabling fast clearing with mouse propagation prevention to avoid opening Unity's object selector.
- Core: `RexTexturePreview` — reusable styled texture preview component featuring an image container and a maximize button to view the texture full-size.
- Texture Repacker: Unified all preview areas (combined preview in PACK, mixed preview in MIX, and channel extraction previews in UNPACK) to use the new `RexTexturePreview` component.
- Texture Repacker: Refactored the UNPACK channel layout to use a 2x2 grid of slot boxes matching the PACK tab, rendering real-time greyscale channel previews with custom suffix inputs, invert toggle buttons, maximize buttons, header checkboxes, and card opacity fading when disabled.
- Core: `RexTextureField` — reusable styled drag-and-drop field for Texture2D assets with support for object pickers, custom color previewing, action events, and multi-line instruction labels ("or click to select").
- Core: Path validation support to `RexFolderSelector` with a `required` parameter/property, empty path border highlight, and a required tip label below the input.
- Quick Shot: Refactored window layout to use a ScrollView for settings, keeping the header, help box, and capture button fixed.
- Quick Shot: Enabled the header help button and added a quick start guide help box explaining tool parameters (Export Path, Capture Mode, Render Scale, Transparent BG, and Post Operations).
- RexToolsStyles: Added `.rex-button` and `.rex-toggle-btn` classes as standard button styling and toggle button styling.
- Quick Shot: Auto Copy setting to automatically copy captured screenshots to the system clipboard (supported on Windows and macOS).
- Core: `RexSlider` — reusable custom slider with configurable snap increment, tick marks, draggable thumb, editable value field, and reset button.
- Core: `RexButton` — reusable button component with label/icon modes, toggle support, hover/press states, and click flash.
- Quick Shot: Refactored render scale slider to use `RexSlider` with 0.25 snap increment.
- Quick Shot: Refactored post-operation toggle buttons (Auto Open, Auto Copy) to use `RexButton`.
- RexSlider reset button now uses `RexButton` with `d_Refresh` icon.
- Core: `RexActionButton` — large action button with custom tint color, hover/press states, click flash, disabled dimming, icon support.
- Quick Shot: Refactored capture button to use `RexActionButton`.
- Git Integration: A background-threaded Git status and management window (stage, commit, push, pull, fetch).
- Git Integration: Interactive branch and remote status label injected directly into the editor play control toolbar.
- Batch Material Processor: A standalone tool for batch setting material shaders and auto-assigning texture maps from a selected directory using customizable suffix rules and dry-run preview.

### Changed
- Git Integration: Optimized checkbox selection/deselection performance by migrating to a bottom-up linear-time O(N) tree sync, optimizing ancestor state checks, and introducing a nested updating state counter to prevent event callback recursion, keeping checkmark updates visual-only in-memory and eliminating UI freezes.
- Git Integration: Removed the repository path display label from the header status box to keep the layout minimal and clean.
- Git Integration: Refactored `GitIntegrationWindow.cs` to separate non-UI Git operations, path parsing, and file deletions into `GitRunner.cs`, reducing the window's size by ~32% and consolidating business logic.
- Git Integration: Migrated the layout of `GitIntegrationWindow.cs` to `GitIntegrationWindow.uxml` and separated all C# inline styles into a dedicated `GitIntegrationStyles.uss` stylesheet, reducing the C# file size by an additional 128 lines (~42% reduction overall).
- Git Integration: Removed horizontal scrollability on the changed files ScrollView to prevent hijacking mouse vertical scrolling, and added ellipsis truncation (`...`) to file path labels.
- Git Integration: Removed the individual row discard (delete) buttons in the changed files list, relying instead on the global "DISCARD SELECTED" button.
- Git Integration: Skip folder entries and list all changed files recursively (using `--untracked-files=all` status mode) while automatically staging, committing, and discarding parent folder `.meta` files to match standard Git client behavior.
- Git Integration: Display generic asset type icons (script, prefab, folder, material, etc.) next to the changed file status prefixes.
- Git Integration: Select the clicked asset in the Project window (in addition to highlighting/pinging it) when clicking a path in the changed files list.
- Git Integration: Hide `.meta` files from the changed files list UI while automatically staging, committing, and discarding them alongside their corresponding source assets.
- Git Integration: Wrapped repository status and changed files foldout in a ScrollView while pinning the operations panel to the bottom of the window (fixed) to prevent UI overflow in smaller editor windows.
- Texture Repacker: Replaced the standard Unity `Toggle` for channel inverting on the PACK tab with the custom toggle `RexButton` component matching the UNPACK tab.
- Texture Repacker: Wrapped the PACK tab channel grid inside a new container titled `"PACK SETTINGS"` to match the container hierarchy and styling of the UNPACK tab.
- Core: Updated `RexTexturePreview` to feature a 1px border, 4px rounded corners, and render the drop component's background color when empty.
- Texture Repacker: Added context-specific placeholder messages ("packed texture preview", "mixed texture preview", "channel preview") to empty preview components and dynamically hide their maximize buttons when no texture is bound.
- Texture Repacker: Refactored the UI to reuse custom `RexSlider`, `RexButton` (for preview modes, channel swizzling, VAL toggles, and mix selectors), and `RexActionButton` components (featuring tab-specific background tints for PACK, UNPACK, and MIX operations), and cleaned up C# inline style properties to use standard USS layout classes.
- Texture Repacker: Adjusted output settings rows in PACK, UNPACK, and MIX tabs to use a compact 50px fixed-width left column for "Name:" and "Path:" labels, allowing text fields and folder selectors to expand and fill the remaining space.
- Texture Repacker: Split channel slots in the PACK tab into distinct **Texture** and **Value** modes, showing only the relevant controls (drop zone, swizzle row, and invert toggle for Texture mode; value slider for Value mode) to simplify the workflow and reduce visual clutter.
- Texture Repacker: Made output folder paths required across PACK, UNPACK, and MIX tabs, defaulting the initial path to "Assets" while auto-detecting and filling the dropped asset's directory path, prompting validation errors, and blocking execution with dialogs if an operation is run with an empty folder path.
- Texture Repacker: Centered the Invert toggle and positioned its label and checkbox closer together inside the PACK slots to improve alignment and layout.
- Texture Repacker: Adjusted the UNPACK mode channel extraction row layout to place checkboxes first, followed by the channel names, and introduced toggle buttons to invert individual channels during unpacking.
- Texture Repacker: Fixed full-size preview maximize button missing/unsupported Unicode icon by replacing it with a styled Image element rendering Unity's built-in "d_Profiler.Open" icon and added a tooltip.
- Animation Event Copier: Refactored the auto-match and copy buttons to use the custom `RexButton` and `RexActionButton` C# components.
- Animation Event Copier: Displays validation warnings within the window if the source or target object does not contain any animation clips or is not a valid model asset (FBX).
- RexSlider: Added 3px left and right padding to improve horizontal spacing.
- Quick Shot: Refactored the export path section to use the custom `RexFoldout` component and standard button/toggle styles for the control row.
- Batch Material Editor: Removed the focus button from the scanner list; the ObjectFields are now interactive and can be clicked to ping/focus the material in the Project window, while remaining read-only.
- Unused Assets Finder: Refactored the subfolders tree view section to use the custom `RexFoldout` component with a scrollable list area inside, wrapped the screen content in a main ScrollView, wrapped the tab results inside a scrollarea with a max-height limit, refactored the scan and delete-all buttons to use the custom `RexActionButton` component, and moved the delete-all button (renamed to "DELETE SELECTED") to the bottom of the window as a full-width sticky action button.
- Unused Assets Finder: Consolidated the legacy IMGUI implementation and the UI Toolkit implementation into a single `UnusedAssetsFinder.cs` file.

### Fixed
- Git Integration: Fixed an `InvalidOperationException` on `TaskCompletionSource` during background process execution by switching to `TrySetResult`, registering active Git processes for termination on assembly reload (`AssemblyReloadEvents.beforeAssemblyReload`), and suppressing completion/callback delegation during domain reloads.
- USS: Resolved unknown USS property warnings (`text-transform`, `gap`, `user-select`, `pointer-events`) and cursor warnings by using margin-based spacing and Unity-supported cursors.
- Core: Fixed unused field/variable warnings in `AnimationEventCopierWindow` and `ShaderGraphSearchExtension`.
- Unused Assets Finder: Replaced obsolete `FindObjectsOfType` calls with `FindObjectsByType`.
- Scene Pack: Fixed layout alignment by adding padding to prevent the list foldout arrow from going out of bounds.

## [0.5.0] - 2026-03-03

### Added
- Texture Repacker: New **Mix** tab for blending textures with modes (Multiply, Add, Screen, etc.).
- Texture Repacker: High-resolution **Live Preview** window for inspecting details at original size.
- ShaderGraph Search: Connection-based node navigator with back/forward traversal and branch memory.

### Fixed
- Texture Repacker: Fixed UI clipping issues and improved field scaling in smaller windows.
- RexToolsStyles: Enhanced responsive layout constraints for multi-column tool grids.

## [0.4.0] - 2026-02-23

### Added
- Quick Shot: High-resolution screenshot utility for Scene and Game views.

## [0.3.0] - 2026-02-23

### Added
- ShaderGraph Organizer: Align context menu (Left, Right, Up, Down) for aligning selected nodes in the ShaderGraph editor.
- ShaderGraph Organizer: Distribute context menu (Horizontal, Vertical) for evenly spacing selected nodes.

## [0.2.0] - 2026-01-27

### Added
- Unused Assets Finder: New UI Toolkit implementation.
- Unused Assets Finder: Collapsible subfolder tree view for recursive asset scans.
- RexToolsStyles: Added new utility classes (`.rex-row`, `.rex-result-item`, `.rex-hidden`) for standardized layouts.

### Changed


### Fixed


## [0.1.0] - 2026-01-25

### Added
- Texture Repacker: A comprehensive tool for RGBA channel packing and unpacking.
- Reusable USS design system for RexTools editor extensions.
- Branded header and interactive help system for tools.

## [0.0.1] - 2026-01-25

### Added

- Initial project setup.
- Initial RexTools utility implementation.
- ShaderGraph Search extension for searching nodes in ShaderGraph.
- New `.agent` workflows for automated documentation and changelog management.
- Standardized `README.md` structure for tool guides.
