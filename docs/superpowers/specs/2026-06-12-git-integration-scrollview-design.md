# Git Integration Scroll View Layout Design

Design spec for wrapping the main contents of the Git Integration tool in a scrollable view to prevent overflow on smaller screens, while keeping the branded header and help box static at the top.

## Goal

Ensure the Git Integration window's main interface elements (repository info, changed files list foldout, and git operations panel) and its "no repository found" state can scale and scroll properly when the editor window size is reduced, without clipping the header or help instructions.

## Proposed Layout Hierarchy

```
Root VisualElement (rex-root-padding)
 ├── RexHeader (Fixed)
 ├── RexHelpBox (Fixed)
 └── ScrollView (flex-grow: 1, vertical scroll, scrollable content)
      ├── mainContentContainer (Active when repository exists)
      │    ├── Repository Info Box
      │    ├── Changed Files Foldout & ScrollView
      │    └── Operations Box & Inputs
      └── noRepoContainer (Active when repository is missing)
           ├── Warning Label
           └── Scan Button
```

## Detailed Changes

### GitIntegrationWindow.cs

Modify `CreateGUI()`:
- Instantiate a new `ScrollView` styled with `flexGrow = 1`, `marginTop = 4`, and `marginBottom = 4` to match standard layout rules.
- Add `mainContentContainer` and `noRepoContainer` to the `ScrollView` instead of the root.
- The `RexHeader` and `RexHelpBox` will remain children of the root visual element.

## Verification

### Manual Verification
- Open the Git Integration window in Unity.
- Verify that the layout renders correctly under both repository-detected and no-repository states.
- Resize the window vertically and confirm that a vertical scrollbar appears and the settings scroll.
- Ensure the header and help box remain pinned at the top and do not scroll.
