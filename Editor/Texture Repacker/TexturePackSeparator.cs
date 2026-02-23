using UnityEngine;
using UnityEditor;
using UnityEngine.UIElements;
using UnityEditor.UIElements;
using System.IO;
using System.Collections.Generic;
using System.Linq;

namespace RexTools.TextureRepacker.Editor
{
    public enum ChannelSource { R, G, B, A }

    /// <summary>
    /// A reusable Drag and Drop field for Texture2D assets.
    /// </summary>
    public class DragAndDropTextureField : VisualElement
    {
        public System.Action<Texture2D> OnTextureChanged;
        private Texture2D currentTexture;
        private Image previewImage;
        private Label placeholderLabel;
        private string labelText;

        public Texture2D Value { get => currentTexture; set => SetTexture(value, true); }

        public DragAndDropTextureField(string label = "Drop Texture", float height = 80)
        {
            labelText = label;
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
            Add(placeholderLabel);

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
        }

        public void SetColor(Color col)
        {
            previewImage.image = null;
            previewImage.style.backgroundColor = col;
            previewImage.style.display = DisplayStyle.Flex;
            placeholderLabel.text = $"Value: {col.r:F2}";
            placeholderLabel.style.color = Color.white;
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
            if (notify) OnTextureChanged?.Invoke(tex);
        }
    }

    /// <summary>
    /// A drop zone for folders to set path output.
    /// </summary>
    public class FolderDropZone : VisualElement
    {
        public System.Action<string> OnPathChanged;
        private Label pathLabel;

        public FolderDropZone(string initialPath = "Drop Folder Here")
        {
            AddToClassList("rex-box");
            style.height = 30;
            style.justifyContent = Justify.Center;
            style.paddingLeft = 10;
            style.marginBottom = 0;

            pathLabel = new Label(initialPath);
            pathLabel.style.color = new Color(0.6f, 0.6f, 0.6f);
            pathLabel.style.fontSize = 11;
            Add(pathLabel);

            RegisterCallback<DragUpdatedEvent>(e => DragAndDrop.visualMode = DragAndDropVisualMode.Copy);
            RegisterCallback<DragPerformEvent>(e => {
                DragAndDrop.AcceptDrag();
                string path = DragAndDrop.paths.FirstOrDefault();
                if (string.IsNullOrEmpty(path)) {
                    var folder = DragAndDrop.objectReferences.FirstOrDefault();
                    if (folder != null) path = AssetDatabase.GetAssetPath(folder);
                }
                if (!string.IsNullOrEmpty(path) && AssetDatabase.IsValidFolder(path)) SetPath(path);
            });
        }

        public void SetPath(string path)
        {
            pathLabel.text = path;
            pathLabel.style.color = Color.white;
            OnPathChanged?.Invoke(path);
        }
    }

    public class TexturePackSeparator : EditorWindow
    {
        private class ChannelSlotData {
            public Texture2D texture;
            public int channelIndex = 0; // 0=R, 1=G, 2=B, 3=A
            public bool invert = false;
            public bool useCustom = false;
            public float customValue = 0.5f;
        }

        private ChannelSlotData[] packSlots = new ChannelSlotData[4] {
            new ChannelSlotData { channelIndex = 0 }, // Red
            new ChannelSlotData { channelIndex = 1 }, // Green
            new ChannelSlotData { channelIndex = 2 }, // Blue
            new ChannelSlotData { channelIndex = 3 }  // Alpha
        };

        private string outputName = "PackedTexture";
        private string outputPath = "";
        private int currentTabIndex = 0;

        // Performance: pixel cache and debounce
        private Dictionary<int, Color[]> pixelCache = new Dictionary<int, Color[]>();
        private bool _previewDirty = false;
        private IVisualElementScheduledItem _previewSchedule;

        // Unpack settings
        private Texture2D unpackSource;
        private bool[] unpackModes = { true, true, true, false }; // R, G, B, A
        private string[] unpackSuffixes = { "_R", "_G", "_B", "_A" };
        private string unpackOutputName = "UnpackedTexture";
        private string unpackOutputPath = "";

        // UI
        private Image combinedPreview;
        private VisualElement packContainer;
        private VisualElement unpackContainer;
        private Button actionButton;
        private Button packTabBtn;
        private Button unpackTabBtn;
        private TextField nameField;
        private FolderDropZone folderZone;
        private TextField unpackNameField;
        private FolderDropZone unpackFolderZone;
        private Texture2D previewBuffer;
        private int debugPreviewMode = 0; // 0=RGBA, 1=R, 2=G, 3=B, 4=A
        private bool showHelp = false;
        private VisualElement helpBox;
        private List<Button> debugButtons = new List<Button>();
        private List<List<Button>> slotChannelButtons = new List<List<Button>>();
        private List<Button> slotValButtons = new List<Button>();
        private List<DragAndDropTextureField> slotDropZones = new List<DragAndDropTextureField>();
        [MenuItem("Tools/Texture Repacker")]
        public static void ShowWindow() {
            var window = GetWindow<TexturePackSeparator>("Texture Repacker");
            window.minSize = new Vector2(400, 750);
        }

        public void CreateGUI()
        {
            VisualElement root = rootVisualElement;
            root.style.paddingLeft = root.style.paddingRight = root.style.paddingTop = root.style.paddingBottom = 12;

            // Load Global Styles
            string[] possiblePaths = {
                "Packages/com.matrixrex.rextools/Editor/RexToolsStyles.uss",
                "Assets/Editor/RexToolsStyles.uss"
            };
            StyleSheet styleSheet = null;
            foreach (var path in possiblePaths) {
                styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(path);
                if (styleSheet != null) break;
            }
            if (styleSheet != null) root.styleSheets.Add(styleSheet);

            // --- BRANDED HEADER ---
            var header = new VisualElement();
            header.AddToClassList("rex-header-row");

            var brandStack = new VisualElement();
            brandStack.AddToClassList("rex-header-stack");

            var brandLabel = new Label("Rex Tools");
            brandLabel.AddToClassList("rex-brand-label");
            brandStack.Add(brandLabel);

            var titleLabel = new Label("Texture Repacker");
            titleLabel.AddToClassList("rex-tool-title");
            brandStack.Add(titleLabel);

            header.Add(brandStack);

            var helpBtn = new Button { text = "?" };
            helpBtn.AddToClassList("rex-help-btn");
            header.Add(helpBtn);

            root.Add(header);
            // --- HELP BOX ---
            helpBox = new VisualElement { style = { display = DisplayStyle.None } };
            helpBox.AddToClassList("rex-box");
            helpBox.AddToClassList("rex-help-box");
            
            var helpTitle = new Label("HOW TO USE:");
            helpTitle.AddToClassList("rex-help-text-title");
            helpBox.Add(helpTitle);

            var packTip = new Label("• PACK: Drop textures into RGBA slots. Swizzle channels with R|G|B|A buttons.");
            packTip.AddToClassList("rex-help-text-item");
            helpBox.Add(packTip);

            var valTip = new Label("• VAL: Toggle to use a raw value instead of a texture channel.");
            valTip.AddToClassList("rex-help-text-item");
            helpBox.Add(valTip);

            var unpackTip = new Label("• UNPACK: Drop a texture to extract its channels into separate files.");
            unpackTip.AddToClassList("rex-help-text-item");
            helpBox.Add(unpackTip);
            
            root.Add(helpBox);

            helpBtn.clicked += () => {
                showHelp = !showHelp;
                helpBox.style.display = showHelp ? DisplayStyle.Flex : DisplayStyle.None;
            };

            // Tabs Header
            var tabs = new VisualElement { style = { flexDirection = FlexDirection.Row, height = 30, marginBottom = 15, flexShrink = 0 } };
            tabs.AddToClassList("rex-tabs-container");
            packTabBtn = CreateTabButton("PACK", 0, tabs);
            unpackTabBtn = CreateTabButton("UNPACK", 1, tabs);
            root.Add(tabs);

            // Scrollable Content Area
            var scrollView = new ScrollView(ScrollViewMode.Vertical);
            scrollView.style.flexGrow = 1;
            root.Add(scrollView);

            // Container Logic
            packContainer = new VisualElement { style = { flexShrink = 0 } };
            unpackContainer = new VisualElement { style = { flexShrink = 0 } };
            scrollView.Add(packContainer);
            scrollView.Add(unpackContainer);

            SetupPackUI();
            SetupUnpackUI();

            // Footer Button (Fixed at Bottom)
            actionButton = new Button { style = { flexShrink = 0 } };
            actionButton.AddToClassList("rex-action-button");
            actionButton.clicked += Process;
            root.Add(actionButton);

            SwitchTab(currentTabIndex);
        }

        private Button CreateTabButton(string label, int index, VisualElement parent)
        {
            var btn = new Button { text = label };
            btn.AddToClassList("rex-tab-button");
            btn.clicked += () => SwitchTab(index);
            parent.Add(btn);
            return btn;
        }

        private void SwitchTab(int index)
        {
            currentTabIndex = index;
            packContainer.style.display = index == 0 ? DisplayStyle.Flex : DisplayStyle.None;
            unpackContainer.style.display = index == 1 ? DisplayStyle.Flex : DisplayStyle.None;
            actionButton.text = index == 0 ? "PACK" : "UNPACK";
            
            actionButton.RemoveFromClassList("rex-action-button--pack");
            actionButton.RemoveFromClassList("rex-action-button--unpack");
            actionButton.AddToClassList(index == 0 ? "rex-action-button--pack" : "rex-action-button--unpack");

            // Toggle Tab Classes
            if (packTabBtn != null) {
                packTabBtn.RemoveFromClassList("rex-tab-button--active");
                packTabBtn.RemoveFromClassList("rex-tab-button--inactive");
                packTabBtn.AddToClassList(index == 0 ? "rex-tab-button--active" : "rex-tab-button--inactive");
            }
            if (unpackTabBtn != null) {
                unpackTabBtn.RemoveFromClassList("rex-tab-button--active");
                unpackTabBtn.RemoveFromClassList("rex-tab-button--inactive");
                unpackTabBtn.AddToClassList(index == 1 ? "rex-tab-button--active" : "rex-tab-button--inactive");
            }
            
            if (index == 0) UpdatePreview();
        }

        private void SetupPackUI()
        {
            slotChannelButtons.Clear();
            slotValButtons.Clear();
            slotDropZones.Clear();
            // --- TOP PREVIEW SECTION ---
            var previewSection = new VisualElement { style = { marginBottom = 15, flexDirection = FlexDirection.Row, justifyContent = Justify.Center, alignItems = Align.Center, flexShrink = 0, height = 180 } };
            
            combinedPreview = new Image { style = { width = 160, height = 160, backgroundColor = Color.black, marginRight = 10 } };
            combinedPreview.scaleMode = ScaleMode.ScaleToFit;
            previewSection.Add(combinedPreview);

            var debugColumn = new VisualElement { style = { flexDirection = FlexDirection.Column, justifyContent = Justify.Center } };
            debugButtons.Clear();
            string[] modes = { "RGBA", "R", "G", "B", "A" };
            for (int i = 0; i < modes.Length; i++) {
                int m = i;
                var btn = new Button { text = modes[i], style = { width = 45, height = 25, fontSize = 9, marginBottom = 4 } };
                btn.AddToClassList("rex-button-small");
                btn.clicked += () => { debugPreviewMode = m; UpdatePreview(); };
                debugButtons.Add(btn);
                debugColumn.Add(btn);
            }
            previewSection.Add(debugColumn);
            packContainer.Add(previewSection);

            // --- SAVE SETTINGS ---
            var saveBox = new VisualElement { style = { flexShrink = 0 } };
            saveBox.AddToClassList("rex-box");
            saveBox.Add(new Label("OUTPUT SETTINGS") { style = { unityFontStyleAndWeight = FontStyle.Bold, fontSize = 10, marginBottom = 5, color = Color.gray } });
            
            var nameRow = new VisualElement();
            nameRow.AddToClassList("rex-row");
            nameRow.style.marginBottom = 5;
            nameRow.Add(new Label("Name:") { style = { width = 40 } });
            nameField = new TextField { value = outputName, style = { flexGrow = 1 } };
            nameField.RegisterValueChangedCallback(e => outputName = e.newValue);
            nameRow.Add(nameField);
            saveBox.Add(nameRow);

            folderZone = new FolderDropZone();
            folderZone.OnPathChanged = p => outputPath = p;
            
            var pathRow = new VisualElement { style = { flexDirection = FlexDirection.Row, alignItems = Align.Center } };
            pathRow.Add(new Label("Path:") { style = { width = 40 } });
            pathRow.Add(folderZone);
            folderZone.style.flexGrow = 1;
            saveBox.Add(pathRow);
            
            packContainer.Add(saveBox);

            // --- CHANNEL SLOTS GRID ---
            var grid = new VisualElement { style = { flexDirection = FlexDirection.Row, flexWrap = Wrap.Wrap, justifyContent = Justify.SpaceBetween, flexShrink = 0 } };
            grid.AddToClassList("rex-grid");
            string[] names = { "RED (R)", "GREEN (G)", "BLUE (B)", "ALPHA (A)" };
            Color[] colors = { new Color(1, 0.3f, 0.3f), new Color(0.3f, 1, 0.3f), new Color(0.3f, 0.6f, 1), Color.white };

            for (int i = 0; i < 4; i++) {
                int index = i;
                var slot = new VisualElement();
                slot.AddToClassList("rex-box");
                slot.style.width = 180;
                slot.Add(new Label(names[i]) { style = { unityFontStyleAndWeight = FontStyle.Bold, fontSize = 10, marginBottom = 4, color = colors[i] } });
                
                var drop = new DragAndDropTextureField();
                drop.OnTextureChanged = tex => {
                    packSlots[index].texture = tex;
                    OnSlotTextureDropped(tex);
                    UpdatePreview();
                };
                slotDropZones.Add(drop);
                slot.Add(drop);

                // Channel Icons Grid [R][G][B][A]
                var iconGrid = new VisualElement { style = { flexDirection = FlexDirection.Row, marginTop = 4, justifyContent = Justify.Center } };
                var slotButtons = new List<Button>();
                for (int c = 0; c < 4; c++) {
                    int chan = c;
                    var btn = new Button { text = modes[c+1], style = { width = 30, height = 20, fontSize = 9, marginRight = 2 } };
                    btn.AddToClassList("rex-button-small");
                    btn.clicked += () => { packSlots[index].channelIndex = chan; UpdatePreview(); };
                    slotButtons.Add(btn);
                    iconGrid.Add(btn);
                }
                slotChannelButtons.Add(slotButtons);
                slot.Add(iconGrid);

                // Controls
                var controls = new VisualElement { style = { paddingLeft = 4, paddingRight = 4, marginTop = 4 } };
                
                var invertToggle = new Toggle("Invert") { value = packSlots[index].invert, style = { fontSize = 9, marginBottom = 2 } };
                invertToggle.RegisterValueChangedCallback(e => { packSlots[index].invert = e.newValue; UpdatePreview(); });
                controls.Add(invertToggle);
                
                var customRow = new VisualElement { style = { flexDirection = FlexDirection.Row, alignItems = Align.Center } };
                var customValBtn = new Button { text = "VAL", style = { fontSize = 8, width = 32, height = 18, marginRight = 4 } };
                customValBtn.AddToClassList("rex-button-small");
                
                var slider = new Slider(0, 1) { value = packSlots[index].customValue, style = { flexGrow = 1, height = 15, marginRight = 4 } };
                var numField = new FloatField { value = packSlots[index].customValue, style = { width = 35, fontSize = 8 } };
                
                customValBtn.clicked += () => {
                    bool newState = !packSlots[index].useCustom;
                    packSlots[index].useCustom = newState;
                    slider.SetEnabled(newState);
                    numField.SetEnabled(newState);
                    UpdatePreview();
                };
                slotValButtons.Add(customValBtn);
               
                slider.RegisterValueChangedCallback(e => { 
                    packSlots[index].customValue = e.newValue; 
                    numField.SetValueWithoutNotify(e.newValue);
                    UpdatePreview(); 
                });
                numField.RegisterValueChangedCallback(e => {
                    float val = Mathf.Clamp01(e.newValue);
                    packSlots[index].customValue = val;
                    slider.SetValueWithoutNotify(val);
                    UpdatePreview();
                });

                slider.SetEnabled(packSlots[index].useCustom);
                numField.SetEnabled(packSlots[index].useCustom);
                
                customRow.Add(customValBtn);
                customRow.Add(slider);
                customRow.Add(numField);
                controls.Add(customRow);
                slot.Add(controls);

                grid.Add(slot);
            }
            packContainer.Add(grid);
        }

        private void OnSlotTextureDropped(Texture2D tex)
        {
            if (tex == null) return;
            // Invalidate cache for this texture so fresh pixels are read
            if (tex != null) pixelCache.Remove(tex.GetInstanceID());
            // Auto name from first dropped texture
            if (outputName == "PackedTexture" || string.IsNullOrEmpty(outputName)) {
                outputName = GenerateBaseName(tex.name);
                nameField.value = outputName;
            }
            // Auto folder from first dropped texture
            if (string.IsNullOrEmpty(outputPath)) {
                outputPath = Path.GetDirectoryName(AssetDatabase.GetAssetPath(tex));
                folderZone.SetPath(outputPath);
            }
        }

        private void SetupUnpackUI()
        {
            // --- SOURCE SECTION ---
            var sourceBox = new VisualElement { style = { flexShrink = 0, marginBottom = 15 } };
            sourceBox.AddToClassList("rex-box");
            sourceBox.Add(new Label("SOURCE TEXTURE") { style = { unityFontStyleAndWeight = FontStyle.Bold, fontSize = 10, marginBottom = 5, color = Color.gray } });
            
            var drop = new DragAndDropTextureField("Drop Texture to Unpack", 100);
            drop.OnTextureChanged = tex => {
                unpackSource = tex;
                if (tex != null) {
                    if (string.IsNullOrEmpty(unpackOutputPath)) {
                        unpackOutputPath = Path.GetDirectoryName(AssetDatabase.GetAssetPath(tex));
                        unpackFolderZone.SetPath(unpackOutputPath);
                    }
                    if (unpackOutputName == "UnpackedTexture") {
                        unpackOutputName = GenerateBaseName(tex.name);
                        unpackNameField.value = unpackOutputName;
                    }
                }
            };
            sourceBox.Add(drop);
            unpackContainer.Add(sourceBox);

            // --- OUTPUT SETTINGS ---
            var outputBox = new VisualElement { style = { flexShrink = 0, marginBottom = 15 } };
            outputBox.AddToClassList("rex-box");
            outputBox.Add(new Label("OUTPUT SETTINGS") { style = { unityFontStyleAndWeight = FontStyle.Bold, fontSize = 10, marginBottom = 5, color = Color.gray } });

            var nameRow = new VisualElement();
            nameRow.AddToClassList("rex-row");
            nameRow.style.marginBottom = 5;
            nameRow.Add(new Label("Name:") { style = { width = 40 } });
            unpackNameField = new TextField { value = unpackOutputName, style = { flexGrow = 1 } };
            unpackNameField.RegisterValueChangedCallback(e => unpackOutputName = e.newValue);
            nameRow.Add(unpackNameField);
            outputBox.Add(nameRow);

            unpackFolderZone = new FolderDropZone();
            unpackFolderZone.OnPathChanged = p => unpackOutputPath = p;
            
            var pathRow = new VisualElement { style = { flexDirection = FlexDirection.Row, alignItems = Align.Center } };
            pathRow.Add(new Label("Path:") { style = { width = 40 } });
            pathRow.Add(unpackFolderZone);
            unpackFolderZone.style.flexGrow = 1;
            outputBox.Add(pathRow);
            unpackContainer.Add(outputBox);

            // --- CHANNELS SECTION ---
            var channelsBox = new VisualElement { style = { flexShrink = 0 } };
            channelsBox.AddToClassList("rex-box");
            channelsBox.Add(new Label("CHANNEL EXTRACTION") { style = { unityFontStyleAndWeight = FontStyle.Bold, fontSize = 10, marginBottom = 5, color = Color.gray } });
            
            string[] names = { "Red", "Green", "Blue", "Alpha" };
            for (int i = 0; i < 4; i++) {
                int idx = i;
                var row = new VisualElement { style = { flexDirection = FlexDirection.Row, alignItems = Align.Center, marginBottom = 4 } };
                var toggle = new Toggle(names[i]) { value = unpackModes[i], style = { flexGrow = 1 } };
                var suffix = new TextField { value = unpackSuffixes[i], style = { width = 60 } };
                toggle.RegisterValueChangedCallback(e => { unpackModes[idx] = e.newValue; suffix.SetEnabled(e.newValue); });
                suffix.RegisterValueChangedCallback(e => unpackSuffixes[idx] = e.newValue);
                row.Add(toggle);
                row.Add(new Label("Suffix: ") { style = { fontSize = 10, color = Color.gray } });
                row.Add(suffix);
                channelsBox.Add(row);
            }
            unpackContainer.Add(channelsBox);
        }

        /// <summary>
        /// Marks the preview as dirty and schedules a deferred rebuild.
        /// Coalesces rapid UI changes (sliders, toggles) into one rebuild per ~100ms.
        /// </summary>
        private void UpdatePreview()
        {
            if (combinedPreview == null) return;

            // Always update button visual states immediately (cheap)
            UpdateButtonStates();

            // Debounce the expensive pixel rebuild
            _previewDirty = true;
            if (_previewSchedule == null) {
                _previewSchedule = rootVisualElement.schedule.Execute(RebuildPreview).Every(100);
            }
        }

        private void UpdateButtonStates()
        {
            for (int i = 0; i < debugButtons.Count; i++) {
                if (debugPreviewMode == i) debugButtons[i].AddToClassList("rex-button-small--active");
                else debugButtons[i].RemoveFromClassList("rex-button-small--active");
            }
            for (int i = 0; i < slotChannelButtons.Count; i++) {
                for (int c = 0; c < slotChannelButtons[i].Count; c++) {
                    if (packSlots[i].channelIndex == c) slotChannelButtons[i][c].AddToClassList("rex-button-small--active");
                    else slotChannelButtons[i][c].RemoveFromClassList("rex-button-small--active");
                }
                if (packSlots[i].useCustom) {
                    slotValButtons[i].AddToClassList("rex-button-small--active");
                    slotDropZones[i].SetColor(new Color(packSlots[i].customValue, packSlots[i].customValue, packSlots[i].customValue, 1f));
                } else {
                    slotValButtons[i].RemoveFromClassList("rex-button-small--active");
                    slotDropZones[i].ClearColor();
                }
            }
        }

        /// <summary>
        /// Performs the actual preview pixel rebuild. Only runs when _previewDirty is set.
        /// Uses float[] arrays and cached bulk pixel reads for speed.
        /// </summary>
        private void RebuildPreview()
        {
            if (!_previewDirty) return;
            _previewDirty = false;

            const int size = 128;
            int totalPixels = size * size;
            if (previewBuffer == null) previewBuffer = new Texture2D(size, size, TextureFormat.RGBA32, false);

            // Sample each slot into a float array (single channel value per pixel)
            float[][] slotValues = new float[4][];
            for (int i = 0; i < 4; i++) {
                slotValues[i] = SampleSlotChannel(packSlots[i], size);
            }

            // Compose final preview pixels
            Color[] pixels = new Color[totalPixels];
            for (int i = 0; i < totalPixels; i++) {
                float r = slotValues[0][i];
                float g = slotValues[1][i];
                float b = slotValues[2][i];
                float a = slotValues[3][i];

                switch (debugPreviewMode) {
                    case 1: pixels[i] = new Color(r, r, r, 1f); break;
                    case 2: pixels[i] = new Color(g, g, g, 1f); break;
                    case 3: pixels[i] = new Color(b, b, b, 1f); break;
                    case 4: pixels[i] = new Color(a, a, a, 1f); break;
                    default: pixels[i] = new Color(r, g, b, a); break;
                }
            }

            previewBuffer.SetPixels(pixels);
            previewBuffer.Apply();
            combinedPreview.image = previewBuffer;
        }

        /// <summary>
        /// Samples a single slot's selected channel into a flat float[] at the given preview size.
        /// Uses cached bulk pixel reads + nearest-neighbor resampling instead of per-pixel GetPixelBilinear.
        /// </summary>
        private float[] SampleSlotChannel(ChannelSlotData slot, int size)
        {
            int totalPixels = size * size;
            float[] result = new float[totalPixels];

            if (slot.useCustom) {
                float val = slot.invert ? 1f - slot.customValue : slot.customValue;
                System.Array.Fill(result, val);
                return result;
            }

            if (slot.texture == null) {
                float val = (slot.channelIndex == 3) ? 1f : 0f;
                System.Array.Fill(result, val);
                return result;
            }

            // Bulk read source pixels via cache (handles non-readable textures via GPU blit)
            Color[] srcPixels = GetReadablePixels(slot.texture);
            int srcW = slot.texture.width;
            int srcH = slot.texture.height;
            int channel = slot.channelIndex;
            bool invert = slot.invert;

            for (int y = 0; y < size; y++) {
                int srcY = Mathf.Clamp(y * srcH / size, 0, srcH - 1);
                for (int x = 0; x < size; x++) {
                    int srcX = Mathf.Clamp(x * srcW / size, 0, srcW - 1);
                    Color p = srcPixels[srcY * srcW + srcX];
                    float val = channel == 0 ? p.r : channel == 1 ? p.g : channel == 2 ? p.b : p.a;
                    if (invert) val = 1f - val;
                    result[y * size + x] = val;
                }
            }
            return result;
        }

        private void Process() {
            if (currentTabIndex == 0) Pack(); else Unpack();
        }

        private void Pack()
        {
            string finalPath = Path.Combine(outputPath ?? "", outputName + ".png").Replace('\\', '/');
            if (File.Exists(finalPath)) {
                if (!EditorUtility.DisplayDialog("File Exists", $"An asset already exists at {finalPath}. Do you want to overwrite it?", "Overwrite", "Cancel"))
                    return;
            }

            int w = 512, h = 512;
            var nonNull = packSlots.Where(s => !s.useCustom && s.texture != null).ToList();
            if (nonNull.Count > 0) {
                w = nonNull.Max(s => s.texture.width);
                h = nonNull.Max(s => s.texture.height);
            }

            foreach (var slot in packSlots) {
                if (!slot.useCustom && slot.texture != null) GetReadablePixels(slot.texture);
            }

            try {
                EditorUtility.DisplayProgressBar("Packing Texture", "Reading source textures...", 0f);

                // Pre-read all source pixels into cached arrays
                Color[][] slotPixels = new Color[4][];
                int[] slotW = new int[4];
                int[] slotH = new int[4];
                for (int c = 0; c < 4; c++) {
                    if (!packSlots[c].useCustom && packSlots[c].texture != null) {
                        slotPixels[c] = GetReadablePixels(packSlots[c].texture);
                        slotW[c] = packSlots[c].texture.width;
                        slotH[c] = packSlots[c].texture.height;
                    }
                }

                Texture2D result = new Texture2D(w, h, TextureFormat.RGBA32, false);
                Color[] pixels = new Color[w * h];

                // Pre-compute custom values
                float[] customVals = new float[4];
                for (int c = 0; c < 4; c++) {
                    if (packSlots[c].useCustom)
                        customVals[c] = packSlots[c].invert ? 1f - packSlots[c].customValue : packSlots[c].customValue;
                    else if (packSlots[c].texture == null)
                        customVals[c] = (c == 3) ? 1f : 0f;
                }

                int progressInterval = Mathf.Max(1, h / 20);
                for (int y = 0; y < h; y++) {
                    if (y % progressInterval == 0)
                        EditorUtility.DisplayProgressBar("Packing Texture", $"Processing row {y}/{h}...", (float)y / h);

                    for (int x = 0; x < w; x++) {
                        float[] channels = new float[4];
                        for (int c = 0; c < 4; c++) {
                            if (packSlots[c].useCustom || packSlots[c].texture == null) {
                                channels[c] = customVals[c];
                            } else {
                                int srcX = Mathf.Clamp(x * slotW[c] / w, 0, slotW[c] - 1);
                                int srcY = Mathf.Clamp(y * slotH[c] / h, 0, slotH[c] - 1);
                                Color p = slotPixels[c][srcY * slotW[c] + srcX];
                                channels[c] = packSlots[c].channelIndex == 0 ? p.r : packSlots[c].channelIndex == 1 ? p.g : packSlots[c].channelIndex == 2 ? p.b : p.a;
                                if (packSlots[c].invert) channels[c] = 1f - channels[c];
                            }
                        }
                        pixels[y * w + x] = new Color(channels[0], channels[1], channels[2], channels[3]);
                    }
                }

                EditorUtility.DisplayProgressBar("Packing Texture", "Encoding PNG...", 0.95f);
                result.SetPixels(pixels);
                result.Apply();

                File.WriteAllBytes(finalPath, result.EncodeToPNG());
                AssetDatabase.Refresh();
                EditorUtility.DisplayDialog("Success", "Texture Saved to: " + finalPath, "OK");
                DestroyImmediate(result);
            } finally {
                EditorUtility.ClearProgressBar();
            }
        }

        private void Unpack()
        {
            if (unpackSource == null) return;
            
            string finalPath = string.IsNullOrEmpty(unpackOutputPath) 
                ? Path.GetDirectoryName(AssetDatabase.GetAssetPath(unpackSource)) 
                : unpackOutputPath;
            
            string name = unpackOutputName;
            
            // Check for existing files first
            bool anyExists = false;
            for (int i = 0; i < 4; i++) {
                if (!unpackModes[i]) continue;
                if (File.Exists(Path.Combine(finalPath, name + unpackSuffixes[i] + ".png"))) {
                    anyExists = true;
                    break;
                }
            }
            if (anyExists && !EditorUtility.DisplayDialog("Overwrite Files", "One or more files already exist. Do you want to overwrite them?", "Overwrite", "Cancel"))
                return;

            // Use cached GPU blit read instead of requiring readable import
            var pixels = GetReadablePixels(unpackSource);
            int w = unpackSource.width;
            int h = unpackSource.height;

            for (int i = 0; i < 4; i++) {
                if (!unpackModes[i]) continue;
                Texture2D res = new Texture2D(w, h, TextureFormat.RGB24, false);
                Color[] resPixels = new Color[pixels.Length];
                for (int p = 0; p < pixels.Length; p++) {
                    float val = i == 0 ? pixels[p].r : i == 1 ? pixels[p].g : i == 2 ? pixels[p].b : pixels[p].a;
                    resPixels[p] = new Color(val, val, val, 1f);
                }
                res.SetPixels(resPixels);
                res.Apply();
                
                string outFilePath = Path.Combine(finalPath, name + unpackSuffixes[i] + ".png").Replace('\\', '/');
                File.WriteAllBytes(outFilePath, res.EncodeToPNG());
                DestroyImmediate(res);
            }
            AssetDatabase.Refresh();
            EditorUtility.DisplayDialog("Success", "Unpacking complete!", "OK");
        }

        /// <summary>
        /// Creates a readable copy of a texture via GPU blit — avoids slow asset reimport for PSD files.
        /// </summary>
        private Texture2D MakeReadableCopy(Texture2D tex)
        {
            RenderTexture tmp = RenderTexture.GetTemporary(tex.width, tex.height, 0, RenderTextureFormat.ARGB32);
            Graphics.Blit(tex, tmp);
            RenderTexture prev = RenderTexture.active;
            RenderTexture.active = tmp;
            Texture2D readable = new Texture2D(tex.width, tex.height, TextureFormat.RGBA32, false);
            readable.ReadPixels(new Rect(0, 0, tex.width, tex.height), 0, 0);
            readable.Apply();
            RenderTexture.active = prev;
            RenderTexture.ReleaseTemporary(tmp);
            return readable;
        }

        /// <summary>
        /// Returns cached pixel array for a texture. Uses GPU blit if texture is not readable.
        /// </summary>
        private Color[] GetReadablePixels(Texture2D tex)
        {
            int id = tex.GetInstanceID();
            if (pixelCache.TryGetValue(id, out var cached)) return cached;

            Color[] pixels;
            if (tex.isReadable) {
                pixels = tex.GetPixels();
            } else {
                var copy = MakeReadableCopy(tex);
                pixels = copy.GetPixels();
                DestroyImmediate(copy);
            }
            pixelCache[id] = pixels;
            return pixels;
        }

        private string GenerateBaseName(string name) {
            string[] suffixes = { "_packed", "_pack", "_combined", "_tex", "_diffuse", "_albedo" };
            foreach (var s in suffixes) if (name.EndsWith(s, System.StringComparison.OrdinalIgnoreCase)) name = name.Substring(0, name.Length - s.Length);
            return name.TrimEnd('_', '-', ' ');
        }
    }
}
