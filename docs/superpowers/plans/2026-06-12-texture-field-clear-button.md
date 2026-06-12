# Texture Field Clear Button Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a "clear" button component to the `RexTextureField` texture assign component.

**Architecture:** Use the existing `RexButton` component with an icon-only setup displaying `remove.png`. Place the button in the upper-right corner of `RexTextureField`. Show the button only when a texture is assigned and not in custom color value mode. Stop propagation of `MouseDownEvent` on the button to prevent triggering the object picker.

**Tech Stack:** C# (Unity 6.0 Editor GUI / UI Toolkit), USS (Unity Style Sheets)

---

### Task 1: Add USS Styles for Texture Field Clear Button

**Files:**
- Modify: `Editor/RexToolsStyles.uss`
- Test: Visual verification in Unity Editor (Texture Repacker window)

- [ ] **Step 1: Update .rex-drag-drop-field and add new classes**
  Verify and update `Editor/RexToolsStyles.uss` to ensure `.rex-drag-drop-field` is `position: relative;` and append clear button styling classes.

  ```css
  /* Drag and Drop Field */
  .rex-drag-drop-field {
      align-items: center;
      justify-content: center;
      background-color: rgba(30, 30, 30, 0.9);
      border-width: 1px;
      border-color: #1a1a1a;
      border-radius: 4px;
      position: relative; /* Add this to support absolute positioning of children */
  }

  /* RexTextureField Clear Button Styles */
  .rex-field-clear-btn {
      position: absolute;
      right: 4px;
      top: 4px;
      width: 20px;
      height: 20px;
      border-width: 0;
      padding: 0;
      background-color: rgba(0, 0, 0, 0.4);
      border-radius: 3px;
  }

  .rex-field-clear-btn:hover {
      background-color: #B33333;
  }

  .rex-field-clear-btn .rex-button__icon {
      width: 10px;
      height: 10px;
  }

  .rex-field-clear-btn:hover .rex-button__icon {
      tint-color: white;
  }
  ```

- [ ] **Step 2: Save the file**

---

### Task 2: Implement Clear Button in RexTextureField

**Files:**
- Modify: `Editor/Core/RexTextureField.cs`
- Test: Build and compilation check in Unity Editor

- [ ] **Step 1: Update RexTextureField class**
  Modify `Editor/Core/RexTextureField.cs` to add a `RexButton` clear button, stop its mouse down propagation, and update its visibility dynamically.

  Replace the content of `Editor/Core/RexTextureField.cs` with:
  ```csharp
  using System;
  using System.Linq;
  using UnityEditor;
  using UnityEngine;
  using UnityEngine.UIElements;

  namespace RexTools.Editor.Core
  {
      /// <summary>
      /// A reusable, styled Drag and Drop field for Texture2D assets in RexTools.
      /// </summary>
      public class RexTextureField : VisualElement
      {
          public Action<Texture2D> OnTextureChanged;
          private Texture2D currentTexture;
          private Image previewImage;
          private Label placeholderLabel;
          private string labelText;
          private RexButton clearBtn;

          public Texture2D Value
          {
              get => currentTexture;
              set => SetTexture(value, true);
          }

          public RexTextureField(string label = "Drop Texture", float height = 80)
          {
              labelText = label + "\nor click to select";
              AddToClassList("rex-drag-drop-field");
              style.height = height;
              style.flexDirection = FlexDirection.Column;
              style.alignItems = Align.Center;
              style.justifyContent = Justify.Center;
              style.minHeight = height;

              previewImage = new Image { scaleMode = ScaleMode.ScaleToFit };
              previewImage.AddToClassList("rex-drag-drop-preview");
              previewImage.style.width = height * 0.7f;
              previewImage.style.height = height * 0.7f;
              previewImage.style.display = DisplayStyle.None;
              Add(previewImage);

              placeholderLabel = new Label(labelText);
              placeholderLabel.AddToClassList("rex-drag-drop-label");
              placeholderLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
              placeholderLabel.style.whiteSpace = WhiteSpace.Normal;
              Add(placeholderLabel);

              // Clear Button Setup using RexButton
              string[] possibleIconPaths = {
                  "Packages/com.matrixrex.rextools/Editor/Icons/remove.png",
                  "Assets/Editor/Icons/remove.png"
              };
              Texture2D removeIcon = null;
              foreach (var path in possibleIconPaths) {
                  removeIcon = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                  if (removeIcon != null) break;
              }

              clearBtn = new RexButton(label: null, icon: removeIcon);
              clearBtn.AddToClassList("rex-field-clear-btn");
              clearBtn.tooltip = "Clear texture";
              clearBtn.OnClick += () => SetTexture(null, true);
              
              // Stop MouseDownEvent propagation to prevent opening Unity Object Picker
              clearBtn.RegisterCallback<MouseDownEvent>(e => e.StopPropagation());
              Add(clearBtn);

              RegisterCallback<DragUpdatedEvent>(e => {
                  DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
                  AddToClassList("rex-drag-drop-field--active");
              });
              RegisterCallback<DragLeaveEvent>(e => RemoveFromClassList("rex-drag-drop-field--active"));
              RegisterCallback<DragPerformEvent>(e => {
                  RemoveFromClassList("rex-drag-drop-field--active");
                  DragAndDrop.AcceptDrag();
                  var tex = DragAndDrop.objectReferences.OfType<Texture2D>().FirstOrDefault();
                  if (tex != null) SetTexture(tex, true);
              });
              RegisterCallback<MouseDownEvent>(e => {
                  if (e.button == 0) EditorGUIUtility.ShowObjectPicker<Texture2D>(currentTexture, false, "", GetHashCode());
              });

              this.schedule.Execute(() => {
                  if (Event.current != null && Event.current.type == EventType.ExecuteCommand && Event.current.commandName == "ObjectSelectorUpdated") {
                      if (EditorGUIUtility.GetObjectPickerControlID() == GetHashCode())
                          SetTexture(EditorGUIUtility.GetObjectPickerObject() as Texture2D, true);
                  }
              }).Every(50);

              UpdateClearButtonVisibility();
          }

          public void SetColor(Color col)
          {
              previewImage.image = null;
              previewImage.style.backgroundColor = col;
              previewImage.style.display = DisplayStyle.Flex;
              placeholderLabel.text = $"Value: {col.r:F2}";
              placeholderLabel.style.color = Color.white;
              UpdateClearButtonVisibility();
          }

          public void ClearColor()
          {
              previewImage.style.backgroundColor = Color.clear;
              SetTexture(currentTexture, false);
          }

          private void SetTexture(Texture2D tex, bool notify = true)
          {
              currentTexture = tex;
              previewImage.style.backgroundColor = Color.clear;
              if (tex != null) {
                  previewImage.image = tex;
                  previewImage.style.display = DisplayStyle.Flex;
                  placeholderLabel.text = tex.name;
                  placeholderLabel.style.color = Color.white;
              } else {
                  previewImage.image = null;
                  previewImage.style.display = DisplayStyle.None;
                  placeholderLabel.text = labelText;
                  placeholderLabel.style.color = new Color(0.5f, 0.5f, 0.5f);
              }
              UpdateClearButtonVisibility();
              if (notify) OnTextureChanged?.Invoke(tex);
          }

          private void UpdateClearButtonVisibility()
          {
              bool isCustomColor = previewImage.style.backgroundColor.value != Color.clear;
              clearBtn.style.display = (currentTexture != null && !isCustomColor) ? DisplayStyle.Flex : DisplayStyle.None;
          }
      }
  }
  ```

- [ ] **Step 2: Save the file**

---

### Task 3: Manual Verification in Unity Editor

- [ ] **Step 1: Check Unity Console for Compilation Errors**
  Open Unity and verify that the editor scripts compile without error.

- [ ] **Step 2: Open Texture Repacker Window**
  Open the window via `Tools/Rex Tools/Texture Repacker`.

- [ ] **Step 3: Verify Clear Button functionality**
  - Verify that all empty drop slots have no clear button.
  - Drop a texture into the red channel slot in PACK. Verify that the clear button appears.
  - Click the clear button. Verify that the texture slot clears and the object selector does **not** pop up.
  - Set the red channel to "Value" mode. Verify that the clear button disappears.
  - Repeat the same steps for UNPACK and MIX.
