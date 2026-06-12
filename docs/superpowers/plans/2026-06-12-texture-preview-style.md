# Texture Preview Styling & Placeholders Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Update the texture preview component to have a border, rounded corners, drop-component background styling, and a dynamic placeholder message when no texture is bound.

**Architecture:** Use USS classes to define visual layout, borders, background colors, and absolute-positioned text layers. Update the C# wrapper `RexTexturePreview` to dynamically toggle the display style of the placeholder and maximize button based on the presence of a texture.

**Tech Stack:** C# (Unity 6.0 Editor GUI / UI Toolkit), USS (Unity Style Sheets)

---

### Task 1: Add USS Styles for Texture Preview

**Files:**
- Modify: `Editor/RexToolsStyles.uss`
- Test: Visual verification in Unity Editor (Texture Repacker window)

- [ ] **Step 1: Append preview styles to the end of the USS file**
  Add class selectors `.rex-texture-preview`, `.rex-texture-preview__image`, and `.rex-texture-preview__placeholder` to `Editor/RexToolsStyles.uss`.

  ```css
  /* RexTexturePreview Component Styles */
  .rex-texture-preview {
      background-color: rgba(30, 30, 30, 0.9);
      border-width: 1px;
      border-color: #1a1a1a;
      border-radius: 4px;
      overflow: hidden;
  }

  .rex-texture-preview__image {
      width: 100%;
      height: 100%;
      background-color: transparent;
  }

  .rex-texture-preview__placeholder {
      position: absolute;
      left: 0;
      top: 0;
      right: 0;
      bottom: 0;
      -unity-text-align: middle-center;
      color: #666666;
      font-size: 10px;
      white-space: normal;
      padding: 8px;
      pointer-events: none;
  }
  ```

- [ ] **Step 2: Save the file**

---

### Task 2: Implement Dynamic Placeholder and USS Classes in RexTexturePreview

**Files:**
- Modify: `Editor/Core/RexTexturePreview.cs`
- Test: Build and compilation check in Unity Editor

- [ ] **Step 1: Modify constructor and properties in RexTexturePreview**
  Update `RexTexturePreview.cs` to add USS classes, add a placeholder label, configure visibility toggling, and implement an `UpdateVisibility()` helper.

  Replace the content of `Editor/Core/RexTexturePreview.cs` with:
  ```csharp
  using System;
  using UnityEditor;
  using UnityEngine;
  using UnityEngine.UIElements;

  namespace RexTools.Editor.Core
  {
      /// <summary>
      /// A reusable, styled texture preview component in RexTools, featuring an image container
      /// and a maximize button to view the texture full-size.
      /// </summary>
      public class RexTexturePreview : VisualElement
      {
          private Image previewImage;
          private Button maxBtn;
          private Label placeholderLabel;

          public Texture image
          {
              get => previewImage.image;
              set
              {
                  previewImage.image = value;
                  UpdateVisibility();
              }
          }

          public Action OnMaximizeClicked;

          public RexTexturePreview(float size = 160, string tooltip = "Show full-size preview", string placeholderText = "No Preview")
          {
              AddToClassList("rex-texture-preview");
              style.width = size;
              style.height = size;
              style.position = Position.Relative;
              style.flexShrink = 0;

              previewImage = new Image();
              previewImage.AddToClassList("rex-texture-preview__image");
              previewImage.scaleMode = ScaleMode.ScaleToFit;
              Add(previewImage);

              placeholderLabel = new Label(placeholderText);
              placeholderLabel.AddToClassList("rex-texture-preview__placeholder");
              Add(placeholderLabel);

              maxBtn = new Button();
              maxBtn.AddToClassList("rex-maximize-btn");
              maxBtn.tooltip = tooltip;
              maxBtn.clicked += () => OnMaximizeClicked?.Invoke();

              var maxIcon = new Image { image = EditorGUIUtility.IconContent("d_Profiler.Open").image, pickingMode = PickingMode.Ignore };
              maxIcon.style.width = 14;
              maxIcon.style.height = 14;
              maxIcon.tintColor = new Color(0.8f, 0.8f, 0.8f, 1f);
              maxBtn.Add(maxIcon);

              Add(maxBtn);

              UpdateVisibility();
          }

          private void UpdateVisibility()
          {
              bool hasImage = previewImage.image != null;
              placeholderLabel.style.display = hasImage ? DisplayStyle.None : DisplayStyle.Flex;
              maxBtn.style.display = hasImage ? DisplayStyle.Flex : DisplayStyle.None;
          }
      }
  }
  ```

- [ ] **Step 2: Save the file**

---

### Task 3: Pass Custom Placeholders from TexturePackSeparator

**Files:**
- Modify: `Editor/Texture Repacker/TexturePackSeparator.cs`
- Test: Open Texture Repacker window in Unity, verify visual aesthetics and functionality

- [ ] **Step 1: Modify Pack Preview Initialization**
  Modify line ~211 of `Editor/Texture Repacker/TexturePackSeparator.cs` to pass `"packed texture preview"`:
  ```csharp
  combinedPreview = new RexTexturePreview(160, "Show full-size preview", "packed texture preview");
  ```

- [ ] **Step 2: Modify Unpack Channel Previews Initialization**
  Modify line ~485 of `Editor/Texture Repacker/TexturePackSeparator.cs` to pass `"channel preview"`:
  ```csharp
  var preview = new RexTexturePreview(90, $"Show full-size {names[idx]} preview", "channel preview");
  ```

- [ ] **Step 3: Modify Mix Preview Initialization**
  Modify line ~918 of `Editor/Texture Repacker/TexturePackSeparator.cs` to pass `"mixed texture preview"`:
  ```csharp
  mixPreviewImage = new RexTexturePreview(160, "Show full-size preview", "mixed texture preview");
  ```

- [ ] **Step 4: Save the file**

---

### Task 4: Manual Verification in Unity Editor

- [ ] **Step 1: Check Unity Console for Compilation Errors**
  Open Unity and verify that the editor scripts compile without error.

- [ ] **Step 2: Open Texture Repacker Window**
  Open the window via `Tools/Rex Tools/Texture Repacker`.

- [ ] **Step 3: Verify Default State (No Textures)**
  - Under the PACK tab, verify the main preview displays a border, rounded corners, drop background color, and the text `"packed texture preview"`. Verify that no maximize button is showing.
  - Under the UNPACK tab, verify the four channel previews display `"channel preview"` and no maximize buttons.
  - Under the MIX tab, verify the preview displays `"mixed texture preview"` and no maximize button.

- [ ] **Step 4: Verify Active State (With Textures)**
  - Drop a texture into a PACK slot or select one. Verify that as soon as the preview rebuilds, the placeholder text hides and the maximize button displays.
  - Drop a texture into the UNPACK source. Verify the channel previews display the extracted channels, hide the placeholders, and show maximize buttons.
  - Drop textures into the MIX slots. Verify that the mixed preview displays, the placeholder hides, and the maximize button shows.
