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

        // Mix settings
        public enum BlendMode { Multiply, Add, Screen, Overlay, Subtract, Divide, Darken, Lighten, SoftLight, HardLight }
        private Texture2D mixBase;
        private Texture2D mixLayer;
        private int mixBaseChannel  = -1; // -1 = Full RGBA, 0-3 = R/G/B/A
        private int mixLayerChannel = -1;
        private BlendMode mixBlendMode = BlendMode.Multiply;
        private float mixOpacity = 1f;
        private string mixOutputName = "MixedTexture";
        private string mixOutputPath = "";

        // UI
        private Image combinedPreview;
        private VisualElement packContainer;
        private VisualElement unpackContainer;
        private VisualElement mixContainer;
        private Button actionButton;
        private Button packTabBtn;
        private Button unpackTabBtn;
        private Button mixTabBtn;
        private TextField nameField;
        private FolderDropZone folderZone;
        private TextField unpackNameField;
        private FolderDropZone unpackFolderZone;
        private TextField mixNameField;
        private FolderDropZone mixFolderZone;
        private Image mixPreviewImage;
        private Texture2D mixPreviewBuffer;
        private bool _mixPreviewDirty = false;
        private IVisualElementScheduledItem _mixPreviewSchedule;
        private Texture2D previewBuffer;
        private int debugPreviewMode = 0; // 0=RGBA, 1=R, 2=G, 3=B, 4=A
        private bool showHelp = false;
        private VisualElement helpBox;
        private List<Button> debugButtons = new List<Button>();
        private List<List<Button>> slotChannelButtons = new List<List<Button>>();
        private List<Button> slotValButtons = new List<Button>();
        private List<DragAndDropTextureField> slotDropZones = new List<DragAndDropTextureField>();
        [MenuItem("Tools/Rex Tools/Texture Repacker")]
        public static void ShowWindow() {
            var window = GetWindow<TexturePackSeparator>("Texture Repacker");
            window.minSize = new Vector2(450, 750);
        }

        public void CreateGUI()
        {
            VisualElement root = rootVisualElement;
            root.style.paddingTop = root.style.paddingBottom = 12;
            root.style.paddingLeft = root.style.paddingRight = 0; // Use internal padding for scrollbar clarity

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
            header.style.paddingLeft = header.style.paddingRight = 12;

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
            helpBox = new VisualElement { style = { display = DisplayStyle.None, marginLeft = 12, marginRight = 12 } };
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

            var mixTip = new Label("• MIX: Drop Base + Layer textures. Select blend mode and optional channel. Click MIX to save result.");
            mixTip.AddToClassList("rex-help-text-item");
            helpBox.Add(mixTip);
            
            root.Add(helpBox);

            helpBtn.clicked += () => {
                showHelp = !showHelp;
                helpBox.style.display = showHelp ? DisplayStyle.Flex : DisplayStyle.None;
            };

            // Tabs Header
            var tabs = new VisualElement { style = { flexDirection = FlexDirection.Row, height = 30, marginBottom = 15, flexShrink = 0, marginLeft = 12, marginRight = 12 } };
            tabs.AddToClassList("rex-tabs-container");
            packTabBtn   = CreateTabButton("PACK",   0, tabs);
            unpackTabBtn = CreateTabButton("UNPACK", 1, tabs);
            mixTabBtn    = CreateTabButton("MIX",    2, tabs);
            root.Add(tabs);

            // Scrollable Content Area
            var scrollView = new ScrollView(ScrollViewMode.Vertical);
            scrollView.style.flexGrow = 1;
            scrollView.contentContainer.style.paddingLeft = 12;
            scrollView.contentContainer.style.paddingRight = 12;
            root.Add(scrollView);

            // Container Logic
            packContainer   = new VisualElement { style = { flexShrink = 0 } };
            unpackContainer = new VisualElement { style = { flexShrink = 0 } };
            mixContainer    = new VisualElement { style = { flexShrink = 0 } };
            scrollView.Add(packContainer);
            scrollView.Add(unpackContainer);
            scrollView.Add(mixContainer);

            SetupPackUI();
            SetupUnpackUI();
            SetupMixUI();

            // Footer Button (Fixed at Bottom)
            actionButton = new Button { style = { flexShrink = 0, marginLeft = 12, marginRight = 12 } };
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
            packContainer.style.display   = index == 0 ? DisplayStyle.Flex : DisplayStyle.None;
            unpackContainer.style.display = index == 1 ? DisplayStyle.Flex : DisplayStyle.None;
            mixContainer.style.display    = index == 2 ? DisplayStyle.Flex : DisplayStyle.None;

            string[] labels = { "PACK", "UNPACK", "MIX" };
            actionButton.text = labels[index];
            
            actionButton.RemoveFromClassList("rex-action-button--pack");
            actionButton.RemoveFromClassList("rex-action-button--unpack");
            actionButton.AddToClassList(index == 0 ? "rex-action-button--pack" : "rex-action-button--unpack");

            // Toggle Tab Classes
            Button[] tabs = { packTabBtn, unpackTabBtn, mixTabBtn };
            for (int t = 0; t < tabs.Length; t++) {
                if (tabs[t] == null) continue;
                tabs[t].RemoveFromClassList("rex-tab-button--active");
                tabs[t].RemoveFromClassList("rex-tab-button--inactive");
                tabs[t].AddToClassList(t == index ? "rex-tab-button--active" : "rex-tab-button--inactive");
            }

            if (index == 0) UpdatePreview();
            if (index == 2) UpdateMixPreview();
        }

        private void SetupPackUI()
        {
            slotChannelButtons.Clear();
            slotValButtons.Clear();
            slotDropZones.Clear();
            // --- TOP PREVIEW SECTION ---
            var previewSection = new VisualElement { style = { marginBottom = 15, flexDirection = FlexDirection.Row, justifyContent = Justify.Center, alignItems = Align.Center, flexShrink = 0, height = 180 } };
            
            var previewWrap = new VisualElement { style = { width = 160, height = 160, marginRight = 10 } };
            combinedPreview = new Image { style = { width = 160, height = 160, backgroundColor = Color.black } };
            combinedPreview.scaleMode = ScaleMode.ScaleToFit;
            previewWrap.Add(combinedPreview);

            var maxBtn = new Button { text = "⛶" };
            maxBtn.AddToClassList("rex-maximize-btn");
            maxBtn.clicked += () => LivePreviewWindow.ShowWindow(this, 0, "Pack Preview");
            previewWrap.Add(maxBtn);

            previewSection.Add(previewWrap);

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
            nameRow.Add(new Label("Name:") { style = { width = 45, flexShrink = 0 } });
            nameField = new TextField { value = outputName };
            nameField.AddToClassList("rex-field-flex");
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
                slot.style.width = 200; // Increased width for better fitting in 450px window
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
            nameRow.Add(new Label("Name:") { style = { width = 45, flexShrink = 0 } });
            unpackNameField = new TextField { value = unpackOutputName };
            unpackNameField.AddToClassList("rex-field-flex");
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
            if (currentTabIndex == 0) Pack();
            else if (currentTabIndex == 1) Unpack();
            else Mix();
        }

        private void Pack()
        {
            string finalPath = Path.Combine(outputPath ?? "", outputName + ".png").Replace('\\', '/');
            if (File.Exists(finalPath)) {
                if (!EditorUtility.DisplayDialog("File Exists", $"An asset already exists at {finalPath}. Do you want to overwrite it?", "Overwrite", "Cancel"))
                    return;
            }

            Texture2D result = GenerateFullResResult(0);
            if (result == null) return;

            try {
                EditorUtility.DisplayProgressBar("Packing Texture", "Encoding PNG...", 0.95f);
                File.WriteAllBytes(finalPath, result.EncodeToPNG());
                AssetDatabase.Refresh();
                EditorUtility.DisplayDialog("Success", "Texture Saved to: " + finalPath, "OK");
            } finally {
                DestroyImmediate(result);
                EditorUtility.ClearProgressBar();
            }
        }

        private Texture2D GenerateFullResResult(int tabIndex)
        {
            int w = 512, h = 512;
            string title = tabIndex == 0 ? "Packing Texture" : "Mixing Texture";

            if (tabIndex == 0) {
                var nonNull = packSlots.Where(s => !s.useCustom && s.texture != null).ToList();
                if (nonNull.Count > 0) {
                    w = nonNull.Max(s => s.texture.width);
                    h = nonNull.Max(s => s.texture.height);
                }
            } else {
                if (mixBase == null) return null;
                w = mixBase.width;
                h = mixBase.height;
            }

            try {
                EditorUtility.DisplayProgressBar(title, "Reading source textures...", 0f);

                Texture2D result = new Texture2D(w, h, TextureFormat.RGBA32, false);
                Color[] pixels = new Color[w * h];

                if (tabIndex == 0) {
                    // --- PACK ENGINE ---
                    Color[][] slotPixels = new Color[4][];
                    int[] slotW = new int[4];
                    int[] slotH = new int[4];
                    float[] customVals = new float[4];

                    for (int c = 0; c < 4; c++) {
                        if (!packSlots[c].useCustom && packSlots[c].texture != null) {
                            slotPixels[c] = GetReadablePixels(packSlots[c].texture);
                            slotW[c] = packSlots[c].texture.width;
                            slotH[c] = packSlots[c].texture.height;
                        }
                        if (packSlots[c].useCustom)
                            customVals[c] = packSlots[c].invert ? 1f - packSlots[c].customValue : packSlots[c].customValue;
                        else if (packSlots[c].texture == null)
                            customVals[c] = (c == 3) ? 1f : 0f;
                    }

                    int progressInterval = Mathf.Max(1, h / 20);
                    for (int y = 0; y < h; y++) {
                        if (y % progressInterval == 0) EditorUtility.DisplayProgressBar(title, $"Processing row {y}/{h}...", (float)y / h);
                        for (int x = 0; x < w; x++) {
                            float[] channels = new float[4];
                            for (int c = 0; c < 4; c++) {
                                if (packSlots[c].useCustom || packSlots[c].texture == null) {
                                    channels[c] = customVals[c];
                                } else {
                                    int srcX = Mathf.Clamp(x * slotW[c] / w, 0, slotW[c] - 1);
                                    int srcY = Mathf.Clamp(y * slotH[c] / h, 0, slotH[c] - 1);
                                    Color p = slotPixels[c][srcY * slotW[c] + srcX];
                                    float val = packSlots[c].channelIndex == 0 ? p.r : packSlots[c].channelIndex == 1 ? p.g : packSlots[c].channelIndex == 2 ? p.b : p.a;
                                    if (packSlots[c].invert) val = 1f - val;
                                    channels[c] = val;
                                }
                            }
                            pixels[y * w + x] = new Color(channels[0], channels[1], channels[2], channels[3]);
                        }
                    }
                } else {
                    // --- MIX ENGINE ---
                    Color[] basePixels = GetReadablePixels(mixBase);
                    Color[] layerPixels = mixLayer != null ? GetReadablePixels(mixLayer) : null;
                    int lW = mixLayer != null ? mixLayer.width : 1;
                    int lH = mixLayer != null ? mixLayer.height : 1;

                    int progressInterval = Mathf.Max(1, h / 20);
                    for (int y = 0; y < h; y++) {
                        if (y % progressInterval == 0) EditorUtility.DisplayProgressBar(title, $"Row {y}/{h}", (float)y / h);
                        for (int x = 0; x < w; x++) {
                            Color bc = basePixels[y * w + x];
                            Color lc = Color.black;
                            if (layerPixels != null) {
                                int lx = Mathf.Clamp(x * lW / w, 0, lW - 1);
                                int ly = Mathf.Clamp(y * lH / h, 0, lH - 1);
                                lc = layerPixels[ly * lW + lx];
                            }
                            Color fb = ApplyChannelSelect(bc, mixBaseChannel);
                            Color fl = ApplyChannelSelect(lc, mixLayerChannel);
                            Color blended = BlendColors(fb, fl, mixBlendMode);
                            pixels[y * w + x] = Color.Lerp(fb, blended, mixOpacity);
                        }
                    }
                }

                result.SetPixels(pixels);
                result.Apply();
                return result;
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

        // ─── MIX TAB ──────────────────────────────────────────────────────────────

        private void SetupMixUI()
        {
            // ── Preview ──
            var previewSection = new VisualElement { style = { marginBottom = 15, flexDirection = FlexDirection.Row, justifyContent = Justify.Center, alignItems = Align.Center, flexShrink = 0, height = 180 } };
            
            var previewWrap = new VisualElement { style = { width = 160, height = 160 } };
            mixPreviewImage = new Image { style = { width = 160, height = 160, backgroundColor = Color.black } };
            mixPreviewImage.scaleMode = ScaleMode.ScaleToFit;
            previewWrap.Add(mixPreviewImage);

            var maxBtn = new Button { text = "⛶" };
            maxBtn.AddToClassList("rex-maximize-btn");
            maxBtn.clicked += () => LivePreviewWindow.ShowWindow(this, 2, "Mix Preview");
            previewWrap.Add(maxBtn);

            previewSection.Add(previewWrap);
            mixContainer.Add(previewSection);

            // ── Blend Mode ──
            var blendBox = new VisualElement { style = { flexShrink = 0, marginBottom = 10 } };
            blendBox.AddToClassList("rex-box");
            blendBox.Add(new Label("BLEND SETTINGS") { style = { unityFontStyleAndWeight = FontStyle.Bold, fontSize = 10, marginBottom = 6, color = Color.gray } });

            var blendRow = new VisualElement();
            blendRow.AddToClassList("rex-row");
            blendRow.Add(new Label("Mode:") { style = { width = 55, flexShrink = 0 } });
            var blendEnum = new EnumField(mixBlendMode);
            blendEnum.AddToClassList("rex-field-flex");
            blendEnum.RegisterValueChangedCallback(e => { mixBlendMode = (BlendMode)e.newValue; UpdateMixPreview(); });
            blendRow.Add(blendEnum);
            blendBox.Add(blendRow);

            var opacityRow = new VisualElement();
            opacityRow.AddToClassList("rex-row");
            opacityRow.Add(new Label("Opacity:") { style = { width = 55, flexShrink = 0 } });
            var opacitySlider = new Slider(0f, 1f) { value = mixOpacity };
            opacitySlider.AddToClassList("rex-field-flex");
            var opacityNum = new FloatField { value = mixOpacity, style = { width = 40, fontSize = 9, marginLeft = 5 } };
            opacitySlider.RegisterValueChangedCallback(e => {
                mixOpacity = e.newValue;
                opacityNum.SetValueWithoutNotify(e.newValue);
                UpdateMixPreview();
            });
            opacityNum.RegisterValueChangedCallback(e => {
                float v = Mathf.Clamp01(e.newValue);
                mixOpacity = v;
                opacitySlider.SetValueWithoutNotify(v);
                UpdateMixPreview();
            });
            opacityRow.Add(opacitySlider);
            opacityRow.Add(opacityNum);
            blendBox.Add(opacityRow);
            mixContainer.Add(blendBox);

            // ── Texture Inputs ──
            string[] texLabels = { "BASE", "LAYER" };
            for (int t = 0; t < 2; t++) {
                int ti = t;
                var box = new VisualElement { style = { flexShrink = 0, marginBottom = 10 } };
                box.AddToClassList("rex-box");
                box.Add(new Label(texLabels[t]) { style = { unityFontStyleAndWeight = FontStyle.Bold, fontSize = 10, marginBottom = 5, color = ti == 0 ? new Color(0.5f, 0.8f, 1f) : new Color(1f, 0.7f, 0.4f) } });

                var drop = new DragAndDropTextureField("Drop " + texLabels[t] + " Texture", 90);
                drop.OnTextureChanged = tex => {
                    if (ti == 0) {
                        mixBase = tex;
                        if (tex != null) { pixelCache.Remove(tex.GetInstanceID()); }
                        // Auto name/path from base texture
                        if (tex != null && (mixOutputName == "MixedTexture" || string.IsNullOrEmpty(mixOutputName))) {
                            mixOutputName = GenerateBaseName(tex.name) + "_mixed";
                            if (mixNameField != null) mixNameField.value = mixOutputName;
                        }
                        if (tex != null && string.IsNullOrEmpty(mixOutputPath)) {
                            mixOutputPath = Path.GetDirectoryName(AssetDatabase.GetAssetPath(tex));
                            if (mixFolderZone != null) mixFolderZone.SetPath(mixOutputPath);
                        }
                    } else {
                        mixLayer = tex;
                        if (tex != null) { pixelCache.Remove(tex.GetInstanceID()); }
                    }
                    UpdateMixPreview();
                };
                box.Add(drop);

                // Channel selector: Full | R | G | B | A
                var chanRow = new VisualElement { style = { flexDirection = FlexDirection.Row, marginTop = 5, alignItems = Align.Center } };
                chanRow.Add(new Label("Channel:") { style = { width = 55, fontSize = 10, color = Color.gray } });
                string[] chanLabels = { "Full", "R", "G", "B", "A" };
                var chanBtns = new List<Button>();
                for (int c = 0; c < 5; c++) {
                    int ci = c;
                    var btn = new Button { text = chanLabels[c], style = { width = 34, height = 20, fontSize = 9, marginRight = 2 } };
                    btn.AddToClassList("rex-button-small");
                    int channelValue = ci - 1; // Full=-1 (ci=0 → -1), R=0, G=1, B=2, A=3
                    btn.clicked += () => {
                        if (ti == 0) mixBaseChannel  = channelValue;
                        else         mixLayerChannel = channelValue;
                        // Update active state
                        for (int j = 0; j < chanBtns.Count; j++) {
                            chanBtns[j].RemoveFromClassList("rex-button-small--active");
                        }
                        chanBtns[ci].AddToClassList("rex-button-small--active");
                        UpdateMixPreview();
                    };
                    chanBtns.Add(btn);
                    chanRow.Add(btn);
                }
                // Default active = Full
                chanBtns[0].AddToClassList("rex-button-small--active");
                box.Add(chanRow);
                mixContainer.Add(box);
            }

            // ── Output Settings ──
            var outBox = new VisualElement { style = { flexShrink = 0 } };
            outBox.AddToClassList("rex-box");
            outBox.Add(new Label("OUTPUT SETTINGS") { style = { unityFontStyleAndWeight = FontStyle.Bold, fontSize = 10, marginBottom = 5, color = Color.gray } });

            var nameRow2 = new VisualElement();
            nameRow2.AddToClassList("rex-row");
            nameRow2.Add(new Label("Name:") { style = { width = 45, flexShrink = 0 } });
            mixNameField = new TextField { value = mixOutputName };
            mixNameField.AddToClassList("rex-field-flex");
            mixNameField.RegisterValueChangedCallback(e => mixOutputName = e.newValue);
            nameRow2.Add(mixNameField);
            outBox.Add(nameRow2);

            mixFolderZone = new FolderDropZone();
            mixFolderZone.OnPathChanged = p => mixOutputPath = p;
            var pathRow2 = new VisualElement { style = { flexDirection = FlexDirection.Row, alignItems = Align.Center } };
            pathRow2.Add(new Label("Path:") { style = { width = 40 } });
            pathRow2.Add(mixFolderZone);
            mixFolderZone.style.flexGrow = 1;
            outBox.Add(pathRow2);
            mixContainer.Add(outBox);
        }

        private void UpdateMixPreview()
        {
            _mixPreviewDirty = true;
            if (_mixPreviewSchedule == null) {
                _mixPreviewSchedule = rootVisualElement.schedule.Execute(RebuildMixPreview).Every(100);
            }
        }

        private void RebuildMixPreview()
        {
            if (!_mixPreviewDirty || mixPreviewImage == null) return;
            _mixPreviewDirty = false;

            const int size = 128;
            int total = size * size;
            if (mixPreviewBuffer == null) mixPreviewBuffer = new Texture2D(size, size, TextureFormat.RGBA32, false);

            Color[] basePixels  = mixBase  != null ? GetReadablePixels(mixBase)  : null;
            Color[] layerPixels = mixLayer != null ? GetReadablePixels(mixLayer) : null;
            int bW = mixBase  != null ? mixBase.width  : 1;
            int bH = mixBase  != null ? mixBase.height : 1;
            int lW = mixLayer != null ? mixLayer.width  : 1;
            int lH = mixLayer != null ? mixLayer.height : 1;

            Color[] pixels = new Color[total];
            for (int y = 0; y < size; y++) {
                int by = Mathf.Clamp(y * bH / size, 0, bH - 1);
                int ly = Mathf.Clamp(y * lH / size, 0, lH - 1);
                for (int x = 0; x < size; x++) {
                    int bx = Mathf.Clamp(x * bW / size, 0, bW - 1);
                    int lx = Mathf.Clamp(x * lW / size, 0, lW - 1);

                    Color bc = basePixels  != null ? basePixels [by * bW + bx] : Color.black;
                    Color lc = layerPixels != null ? layerPixels[ly * lW + lx] : Color.black;

                    Color finalBase  = ApplyChannelSelect(bc, mixBaseChannel);
                    Color finalLayer = ApplyChannelSelect(lc, mixLayerChannel);

                    Color blended = BlendColors(finalBase, finalLayer, mixBlendMode);
                    pixels[y * size + x] = Color.Lerp(finalBase, blended, mixOpacity);
                }
            }

            mixPreviewBuffer.SetPixels(pixels);
            mixPreviewBuffer.Apply();
            mixPreviewImage.image = mixPreviewBuffer;
        }

        /// <summary>Converts a pixel to a greyscale-spread version when a single channel is selected.</summary>
        private Color ApplyChannelSelect(Color c, int channel)
        {
            if (channel < 0) return c; // Full
            float v = channel == 0 ? c.r : channel == 1 ? c.g : channel == 2 ? c.b : c.a;
            return new Color(v, v, v, 1f);
        }

        private Color BlendColors(Color b, Color l, BlendMode mode)
        {
            Color result = b;
            switch (mode) {
                case BlendMode.Multiply:  result = new Color(b.r*l.r, b.g*l.g, b.b*l.b, b.a*l.a); break;
                case BlendMode.Add:       result = new Color(Mathf.Clamp01(b.r+l.r), Mathf.Clamp01(b.g+l.g), Mathf.Clamp01(b.b+l.b), Mathf.Clamp01(b.a+l.a)); break;
                case BlendMode.Screen:    result = new Color(1-(1-b.r)*(1-l.r), 1-(1-b.g)*(1-l.g), 1-(1-b.b)*(1-l.b), 1-(1-b.a)*(1-l.a)); break;
                case BlendMode.Subtract:  result = new Color(Mathf.Clamp01(b.r-l.r), Mathf.Clamp01(b.g-l.g), Mathf.Clamp01(b.b-l.b), Mathf.Clamp01(b.a-l.a)); break;
                case BlendMode.Divide:    result = new Color(Mathf.Clamp01(l.r < 0.001f ? 1f : b.r/l.r), Mathf.Clamp01(l.g < 0.001f ? 1f : b.g/l.g), Mathf.Clamp01(l.b < 0.001f ? 1f : b.b/l.b), Mathf.Clamp01(l.a < 0.001f ? 1f : b.a/l.a)); break;
                case BlendMode.Darken:    result = new Color(Mathf.Min(b.r,l.r), Mathf.Min(b.g,l.g), Mathf.Min(b.b,l.b), Mathf.Min(b.a,l.a)); break;
                case BlendMode.Lighten:   result = new Color(Mathf.Max(b.r,l.r), Mathf.Max(b.g,l.g), Mathf.Max(b.b,l.b), Mathf.Max(b.a,l.a)); break;
                case BlendMode.Overlay:   result = new Color(OverlayChannel(b.r,l.r), OverlayChannel(b.g,l.g), OverlayChannel(b.b,l.b), OverlayChannel(b.a,l.a)); break;
                case BlendMode.SoftLight: result = new Color(SoftLightChannel(b.r,l.r), SoftLightChannel(b.g,l.g), SoftLightChannel(b.b,l.b), SoftLightChannel(b.a,l.a)); break;
                case BlendMode.HardLight: result = new Color(OverlayChannel(l.r,b.r), OverlayChannel(l.g,b.g), OverlayChannel(l.b,b.b), OverlayChannel(l.a,b.a)); break;
            }
            return result;
        }

        private float OverlayChannel(float b, float l)
            => b < 0.5f ? 2f * b * l : 1f - 2f * (1f - b) * (1f - l);

        private float SoftLightChannel(float b, float l)
            => l < 0.5f
                ? b - (1f - 2f * l) * b * (1f - b)
                : b + (2f * l - 1f) * (D(b) - b);

        private float D(float b) => b <= 0.25f ? ((16f * b - 12f) * b + 4f) * b : Mathf.Sqrt(b);

        private void Mix()
        {
            if (mixBase == null) {
                EditorUtility.DisplayDialog("Mix", "Please drop a Base texture first.", "OK");
                return;
            }

            string finalPath = Path.Combine(
                string.IsNullOrEmpty(mixOutputPath) ? Path.GetDirectoryName(AssetDatabase.GetAssetPath(mixBase)) : mixOutputPath,
                (string.IsNullOrEmpty(mixOutputName) ? "MixedTexture" : mixOutputName) + ".png"
            ).Replace('\\', '/');

            if (File.Exists(finalPath)) {
                if (!EditorUtility.DisplayDialog("File Exists", $"An asset already exists at {finalPath}. Overwrite?", "Overwrite", "Cancel"))
                    return;
            }

            Texture2D result = GenerateFullResResult(2);
            if (result == null) return;

            try {
                EditorUtility.DisplayProgressBar("Mixing Textures", "Encoding PNG...", 0.95f);
                File.WriteAllBytes(finalPath, result.EncodeToPNG());
                AssetDatabase.Refresh();
                EditorUtility.DisplayDialog("Success", "Mixed texture saved to:\n" + finalPath, "OK");
            } finally {
                DestroyImmediate(result);
                EditorUtility.ClearProgressBar();
            }
        }

        // ──────────────────────────────────────────────────────────────────────────

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

        // ──────────────────────────────────────────────────────────────────────────
        // LIVE PREVIEW POPUP
        // ──────────────────────────────────────────────────────────────────────────

        public class LivePreviewWindow : EditorWindow
        {
            private Image _image;
            private TexturePackSeparator _owner;
            private int _mode;
            private Texture2D _highResTexture;
            private Label _resLabel;

            public static void ShowWindow(TexturePackSeparator owner, int mode, string title)
            {
                var window = GetWindow<LivePreviewWindow>("Rex Tools - " + title);
                window._owner = owner;
                window._mode = mode;
                window.minSize = new Vector2(512, 512);
                window.RefreshHighRes();
            }

            private void CreateGUI()
            {
                var toolbar = new VisualElement { style = { flexDirection = FlexDirection.Row, backgroundColor = new Color(0.2f, 0.2f, 0.2f), paddingLeft = 5, paddingRight = 5, height = 25, alignItems = Align.Center } };
                
                var refreshBtn = new Button { text = "Refresh High-Res", style = { height = 20, fontSize = 10 } };
                refreshBtn.clicked += RefreshHighRes;
                toolbar.Add(refreshBtn);

                _resLabel = new Label("Resolution: -") { style = { marginLeft = 10, fontSize = 10, color = Color.gray } };
                toolbar.Add(_resLabel);

                rootVisualElement.Add(toolbar);

                _image = new Image { scaleMode = ScaleMode.ScaleToFit };
                _image.style.flexGrow = 1;
                _image.style.backgroundColor = new Color(0.1f, 0.1f, 0.1f);
                rootVisualElement.Add(_image);
                
                if (_highResTexture != null) RefreshHighRes();
            }

            public void RefreshHighRes()
            {
                if (_owner == null) return;

                if (_highResTexture != null) DestroyImmediate(_highResTexture);
                _highResTexture = _owner.GenerateFullResResult(_mode);

                if (_highResTexture != null) {
                    if (_image != null) _image.image = _highResTexture;
                    if (_resLabel != null) _resLabel.text = $"Resolution: {_highResTexture.width} x {_highResTexture.height}";
                }
            }

            private void OnDestroy()
            {
                if (_highResTexture != null) DestroyImmediate(_highResTexture);
            }
        }
    }
}
