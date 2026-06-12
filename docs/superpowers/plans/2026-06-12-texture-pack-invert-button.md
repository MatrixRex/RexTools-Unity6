# Texture Pack Invert Button & Container Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** On the PACK tab, replace the standard Unity `Toggle` for channel inverting with a custom toggle `RexButton` matching the UNPACK tab, and wrap the channel grid in a `rex-box` container titled `"PACK SETTINGS"`.

**Architecture:** Use `RexButton` inside the slot texture container for inverting. Wrap the channels grid inside a new `VisualElement` styled as a `rex-box` with a bold `"PACK SETTINGS"` label header.

**Tech Stack:** C# (Unity 6.0 Editor GUI / UI Toolkit)

---

### Task 1: Replace Toggle and Wrap Grid in TexturePackSeparator

**Files:**
- Modify: `Editor/Texture Repacker/TexturePackSeparator.cs:265-374`
- Test: Build and compilation check in Unity Editor

- [ ] **Step 1: Replace grid container and toggle code**
  Modify the `SetupPackUI` method in `Editor/Texture Repacker/TexturePackSeparator.cs` to wrap the grid in `channelsBox` and use `RexButton` instead of `Toggle`.

  Replace:
  ```csharp
              // --- CHANNEL SLOTS GRID ---
              var grid = new VisualElement { style = { flexDirection = FlexDirection.Row, flexWrap = Wrap.Wrap, justifyContent = Justify.SpaceBetween, flexShrink = 0 } };
              grid.AddToClassList("rex-grid");
              string[] names = { "RED (R)", "GREEN (G)", "BLUE (B)", "ALPHA (A)" };
              Color[] colors = { new Color(1, 0.3f, 0.3f), new Color(0.3f, 1, 0.3f), new Color(0.3f, 0.6f, 1), Color.white };

              for (int i = 0; i < 4; i++) {
                  int index = i;
                  var slot = new VisualElement();
                  slot.AddToClassList("rex-box");
                  slot.style.width = 200; // Increased width for better fitting in 450px window
                  slot.Add(new Label(names[i]) { style = { unityFontStyleAndWeight = FontStyle.Bold, fontSize = 10, marginBottom = 4, color = colors[i] } });
                  
                  // Mode Selector Row
                  var modeSelectorRow = new VisualElement();
                  modeSelectorRow.AddToClassList("rex-row");
                  modeSelectorRow.style.marginBottom = 6;
                  
                  var texModeBtn = new RexButton("Texture");
                  texModeBtn.style.flexGrow = 1;
                  texModeBtn.style.height = 18;
                  texModeBtn.style.fontSize = 9;
                  
                  var valModeBtn = new RexButton("Value");
                  valModeBtn.style.flexGrow = 1;
                  valModeBtn.style.height = 18;
                  valModeBtn.style.fontSize = 9;
                  
                  modeSelectorRow.Add(texModeBtn);
                  modeSelectorRow.Add(valModeBtn);
                  slot.Add(modeSelectorRow);

                  // Texture Container (drop field, channel buttons, invert toggle)
                  var textureContainer = new VisualElement();
                  textureContainer.style.marginTop = 4;
                  
                  var drop = new RexTextureField();
                  drop.OnTextureChanged = tex => {
                      packSlots[index].texture = tex;
                      OnSlotTextureDropped(tex);
                      UpdatePreview();
                  };
                  slotDropZones.Add(drop);
                  textureContainer.Add(drop);

                  // Channel Icons Grid [R][G][B][A]
                  var iconGrid = new VisualElement { style = { flexDirection = FlexDirection.Row, marginTop = 6, justifyContent = Justify.Center } };
                  var slotButtons = new List<RexButton>();
                  for (int c = 0; c < 4; c++) {
                      int chan = c;
                      var btn = new RexButton(modes[c+1]);
                      btn.style.width = 30;
                      btn.style.height = 20;
                      btn.style.fontSize = 9;
                      btn.style.marginRight = 2;
                      btn.OnClick += () => { packSlots[index].channelIndex = chan; UpdatePreview(); };
                      slotButtons.Add(btn);
                      iconGrid.Add(btn);
                  }
                  slotChannelButtons.Add(slotButtons);
                  textureContainer.Add(iconGrid);

                  var invertToggle = new Toggle("Invert") { value = packSlots[index].invert, style = { fontSize = 9, marginTop = 6, marginBottom = 2 } };
                  invertToggle.AddToClassList("rex-toggle-centered");
                  invertToggle.RegisterValueChangedCallback(e => { packSlots[index].invert = e.newValue; UpdatePreview(); });
                  textureContainer.Add(invertToggle);
                  
                  slot.Add(textureContainer);
                  slotTextureContainers.Add(textureContainer);

                  // Value Container (slider)
                  var valueContainer = new VisualElement();
                  valueContainer.style.marginTop = 4;
                  
                  var slider = new RexSlider(0f, 1f, defaultValue: 0.5f, value: packSlots[index].customValue);
                  slider.AddToClassList("rex-field-flex");
                  slider.style.height = 28; // Default RexSlider height is 28px
                  slider.OnValueChanged += val => {
                      packSlots[index].customValue = val;
                      UpdatePreview();
                  };
                  valueContainer.Add(slider);
                  slotSliders.Add(slider);
                  
                  slot.Add(valueContainer);
                  slotValueContainers.Add(valueContainer);
                  
                  // Click handlers for mode buttons
                  texModeBtn.OnClick += () => {
                      packSlots[index].useCustom = false;
                      UpdatePreview();
                      UpdateButtonStates();
                  };
                  valModeBtn.OnClick += () => {
                      packSlots[index].useCustom = true;
                      UpdatePreview();
                      UpdateButtonStates();
                  };
                  
                  slotTexModeButtons.Add(texModeBtn);
                  slotValModeButtons.Add(valModeBtn);

                  // Initial visibility
                  textureContainer.style.display = packSlots[index].useCustom ? DisplayStyle.None : DisplayStyle.Flex;
                  valueContainer.style.display = packSlots[index].useCustom ? DisplayStyle.Flex : DisplayStyle.None;

                  grid.Add(slot);
              }
              packContainer.Add(grid);
  ```

  With:
  ```csharp
              // --- CHANNEL SLOTS GRID ---
              var channelsBox = new VisualElement { style = { flexShrink = 0 } };
              channelsBox.AddToClassList("rex-box");
              channelsBox.Add(new Label("PACK SETTINGS") { style = { unityFontStyleAndWeight = FontStyle.Bold, fontSize = 10, marginBottom = 5, color = Color.gray } });

              var grid = new VisualElement { style = { flexDirection = FlexDirection.Row, flexWrap = Wrap.Wrap, justifyContent = Justify.SpaceBetween, flexShrink = 0 } };
              grid.AddToClassList("rex-grid");
              string[] names = { "RED (R)", "GREEN (G)", "BLUE (B)", "ALPHA (A)" };
              Color[] colors = { new Color(1, 0.3f, 0.3f), new Color(0.3f, 1, 0.3f), new Color(0.3f, 0.6f, 1), Color.white };

              for (int i = 0; i < 4; i++) {
                  int index = i;
                  var slot = new VisualElement();
                  slot.AddToClassList("rex-box");
                  slot.style.width = 200; // Increased width for better fitting in 450px window
                  slot.Add(new Label(names[i]) { style = { unityFontStyleAndWeight = FontStyle.Bold, fontSize = 10, marginBottom = 4, color = colors[i] } });
                  
                  // Mode Selector Row
                  var modeSelectorRow = new VisualElement();
                  modeSelectorRow.AddToClassList("rex-row");
                  modeSelectorRow.style.marginBottom = 6;
                  
                  var texModeBtn = new RexButton("Texture");
                  texModeBtn.style.flexGrow = 1;
                  texModeBtn.style.height = 18;
                  texModeBtn.style.fontSize = 9;
                  
                  var valModeBtn = new RexButton("Value");
                  valModeBtn.style.flexGrow = 1;
                  valModeBtn.style.height = 18;
                  valModeBtn.style.fontSize = 9;
                  
                  modeSelectorRow.Add(texModeBtn);
                  modeSelectorRow.Add(valModeBtn);
                  slot.Add(modeSelectorRow);

                  // Texture Container (drop field, channel buttons, invert toggle)
                  var textureContainer = new VisualElement();
                  textureContainer.style.marginTop = 4;
                  
                  var drop = new RexTextureField();
                  drop.OnTextureChanged = tex => {
                      packSlots[index].texture = tex;
                      OnSlotTextureDropped(tex);
                      UpdatePreview();
                  };
                  slotDropZones.Add(drop);
                  textureContainer.Add(drop);

                  // Channel Icons Grid [R][G][B][A]
                  var iconGrid = new VisualElement { style = { flexDirection = FlexDirection.Row, marginTop = 6, justifyContent = Justify.Center } };
                  var slotButtons = new List<RexButton>();
                  for (int c = 0; c < 4; c++) {
                      int chan = c;
                      var btn = new RexButton(modes[c+1]);
                      btn.style.width = 30;
                      btn.style.height = 20;
                      btn.style.fontSize = 9;
                      btn.style.marginRight = 2;
                      btn.OnClick += () => { packSlots[index].channelIndex = chan; UpdatePreview(); };
                      slotButtons.Add(btn);
                      iconGrid.Add(btn);
                  }
                  slotChannelButtons.Add(slotButtons);
                  textureContainer.Add(iconGrid);

                  // Row for Invert button
                  var invertRow = new VisualElement();
                  invertRow.AddToClassList("rex-row");
                  invertRow.style.justifyContent = Justify.Center;
                  invertRow.style.marginTop = 6;
                  invertRow.style.marginBottom = 2;

                  var invertBtn = new RexButton("Invert Channel", isToggle: true, defaultActive: packSlots[index].invert);
                  invertBtn.style.flexGrow = 1;
                  invertBtn.style.height = 18;
                  invertBtn.style.fontSize = 9;
                  invertBtn.OnToggleChanged += active => {
                      packSlots[index].invert = active;
                      UpdatePreview();
                  };
                  invertRow.Add(invertBtn);
                  textureContainer.Add(invertRow);
                  
                  slot.Add(textureContainer);
                  slotTextureContainers.Add(textureContainer);

                  // Value Container (slider)
                  var valueContainer = new VisualElement();
                  valueContainer.style.marginTop = 4;
                  
                  var slider = new RexSlider(0f, 1f, defaultValue: 0.5f, value: packSlots[index].customValue);
                  slider.AddToClassList("rex-field-flex");
                  slider.style.height = 28; // Default RexSlider height is 28px
                  slider.OnValueChanged += val => {
                      packSlots[index].customValue = val;
                      UpdatePreview();
                  };
                  valueContainer.Add(slider);
                  slotSliders.Add(slider);
                  
                  slot.Add(valueContainer);
                  slotValueContainers.Add(valueContainer);
                  
                  // Click handlers for mode buttons
                  texModeBtn.OnClick += () => {
                      packSlots[index].useCustom = false;
                      UpdatePreview();
                      UpdateButtonStates();
                  };
                  valModeBtn.OnClick += () => {
                      packSlots[index].useCustom = true;
                      UpdatePreview();
                      UpdateButtonStates();
                  };
                  
                  slotTexModeButtons.Add(texModeBtn);
                  slotValModeButtons.Add(valModeBtn);

                  // Initial visibility
                  textureContainer.style.display = packSlots[index].useCustom ? DisplayStyle.None : DisplayStyle.Flex;
                  valueContainer.style.display = packSlots[index].useCustom ? DisplayStyle.Flex : DisplayStyle.None;

                  grid.Add(slot);
              }
              channelsBox.Add(grid);
              packContainer.Add(channelsBox);
  ```

- [ ] **Step 2: Save the file**

---

### Task 2: Manual Verification in Unity Editor

- [ ] **Step 1: Check Unity Console for Compilation Errors**
  Open Unity and verify that the editor scripts compile without error.

- [ ] **Step 2: Open Texture Repacker Window**
  Open the window via `Tools/Rex Tools/Texture Repacker`.

- [ ] **Step 3: Verify Layout & Invert Functionality**
  - Verify that the four channel boxes are nested inside a single container titled `"PACK SETTINGS"`.
  - Verify that each slot card contains a full-width `"Invert Channel"` button instead of the old check toggle.
  - Bind a texture, click the `"Invert Channel"` button, and verify that the preview updates to show the inverted channel values.
