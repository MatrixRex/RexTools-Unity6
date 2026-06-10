# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [0.5.0] - 2026-03-03

### Added
- Texture Repacker: New **Mix** tab for blending textures with modes (Multiply, Add, Screen, etc.).
- Texture Repacker: High-resolution **Live Preview** window for inspecting details at original size.
- ShaderGraph Search: Connection-based node navigator with back/forward traversal and branch memory.

### Fixed
- Texture Repacker: Fixed UI clipping issues and improved field scaling in smaller windows.
- RexToolsStyles: Enhanced responsive layout constraints for multi-column tool grids.

## [Unreleased]

### Added
- RexToolsStyles: Added `.rex-button` and `.rex-toggle-btn` classes as standard button styling and toggle button styling.
- Quick Shot: Auto Copy setting to automatically copy captured screenshots to the system clipboard (supported on Windows and macOS).
- Core: `RexSlider` — reusable custom slider with configurable snap increment, tick marks, draggable thumb, editable value field, and reset button.
- Core: `RexButton` — reusable button component with label/icon modes, toggle support, hover/press states, and click flash.
- Quick Shot: Refactored render scale slider to use `RexSlider` with 0.25 snap increment.
- Quick Shot: Refactored post-operation toggle buttons (Auto Open, Auto Copy) to use `RexButton`.
- RexSlider reset button now uses `RexButton` with `d_Refresh` icon.
- Git Integration: A background-threaded Git status and management window (stage, commit, push, pull, fetch).
- Git Integration: Interactive branch and remote status label injected directly into the editor play control toolbar.
- Batch Material Processor: A standalone tool for batch setting material shaders and auto-assigning texture maps from a selected directory using customizable suffix rules and dry-run preview.

### Changed
- Quick Shot: Refactored the export path section to use the custom `RexFoldout` component and standard button/toggle styles for the control row.
- Batch Material Editor: Removed the focus button from the scanner list; the ObjectFields are now interactive and can be clicked to ping/focus the material in the Project window, while remaining read-only.

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
