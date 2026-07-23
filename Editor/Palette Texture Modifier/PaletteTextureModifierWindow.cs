using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using RexTools.Editor.Core;

namespace RexTools.PaletteTextureModifier.Editor
{
    public class PaletteTextureModifierWindow : EditorWindow
    {
        [SerializeField] private PaletteData paletteData;

        private Texture2D targetTexture;
        private RexTextureField textureField;
        private Label textureInfoLabel;
        private VisualElement readWriteWarningBox;

        private IntegerField gridColsField;
        private IntegerField gridRowsField;
        private PaletteCanvasElement canvasElement;

        private VisualElement cellEditContainer;
        private Label selectionInfoLabel;
        private ColorField colorPickerField;
        private TextField hexColorField;
        private RexButton mergeBtn;
        private RexButton splitBtn;
        private Toggle gridLinesToggle;

        private RexActionButton saveButton;

        [MenuItem("Tools/Rex Tools/Palette Texture Modifier")]
        public static void ShowWindow()
        {
            var window = GetWindow<PaletteTextureModifierWindow>("Palette Texture Modifier");
            window.minSize = new Vector2(380, 620);
        }

        private void OnEnable()
        {
            if (paletteData == null)
            {
                paletteData = ScriptableObject.CreateInstance<PaletteData>();
                paletteData.InitializeGrid(8, 8);
            }
            Undo.undoRedoPerformed += OnUndoRedo;
        }

        private void OnDisable()
        {
            Undo.undoRedoPerformed -= OnUndoRedo;
        }

        private void OnUndoRedo()
        {
            if (canvasElement != null && paletteData != null)
            {
                canvasElement.MarkDirtyRepaint();
                UpdateCellEditPanel();
            }
        }

        public void CreateGUI()
        {
            VisualElement root = rootVisualElement;
            root.AddToClassList("rex-root-padding");

            // Load Global Styles & Local Styles
            string[] possibleGlobalPaths = {
                "Packages/com.matrixrex.rextools/Editor/RexToolsStyles.uss",
                "Assets/Editor/RexToolsStyles.uss"
            };
            foreach (var path in possibleGlobalPaths)
            {
                var globalSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(path);
                if (globalSheet != null) { root.styleSheets.Add(globalSheet); break; }
            }

            string[] possibleLocalPaths = {
                "Packages/com.matrixrex.rextools/Editor/Palette Texture Modifier/PaletteTextureModifier.uss",
                "Assets/Editor/Palette Texture Modifier/PaletteTextureModifier.uss"
            };
            foreach (var path in possibleLocalPaths)
            {
                var localSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(path);
                if (localSheet != null) { root.styleSheets.Add(localSheet); break; }
            }

            // --- BRANDED HEADER & HELP BOX ---
            var helpBox = new RexHelpBox(
                "Source Texture: Select a palette texture (e.g. 16x16, 32x32, 64x64).",
                "Grid Setup: Specify Grid Columns and Rows or choose a preset grid size.",
                "Selecting Cells: Click or drag-select cells. Hold Shift to select multiple cells. Alt-click to sample color.",
                "Color Editing: Use the Color Picker or Hex input to alter selected cell colors live.",
                "Merge & Split: Select adjacent cells and click Merge Cells. Click Split to revert merged cells.",
                "Save & Overwrite: Writes edited colors directly back to the original PNG texture file.",
                "Undo / Redo: Fully supported via standard Ctrl+Z / Cmd+Z shortcuts."
            );

            var header = new RexHeader("Palette Texture Modifier", showHelpButton: true);
            bool showHelp = false;
            header.OnHelpClicked += () =>
            {
                showHelp = !showHelp;
                helpBox.ToggleVisibility();
                header.SetHelpButtonActive(showHelp);
            };

            root.Add(header);
            root.Add(helpBox);

            // --- SCROLLABLE CONTENT AREA ---
            var scrollView = new ScrollView(ScrollViewMode.Vertical);
            scrollView.style.flexGrow = 1;
            scrollView.style.marginTop = 4;
            scrollView.style.marginBottom = 4;
            root.Add(scrollView);

            // --- 1. TARGET TEXTURE SECTION ---
            var texBox = new VisualElement();
            texBox.AddToClassList("rex-box");

            var texHeaderRow = new VisualElement();
            texHeaderRow.AddToClassList("rex-row");
            texHeaderRow.style.justifyContent = Justify.SpaceBetween;

            var texLabel = new Label("PALETTE TEXTURE");
            texLabel.AddToClassList("rex-section-label");
            texHeaderRow.Add(texLabel);

            var presetBtnContainer = new VisualElement();
            texHeaderRow.Add(presetBtnContainer);
            texBox.Add(texHeaderRow);

            textureField = new RexTextureField("Drop Palette Texture", 75);
            textureField.OnTextureChanged += OnTextureAssigned;
            texBox.Add(textureField);

            textureInfoLabel = new Label("No texture assigned");
            textureInfoLabel.AddToClassList("palette-info-label");
            texBox.Add(textureInfoLabel);

            readWriteWarningBox = new VisualElement();
            readWriteWarningBox.AddToClassList("rex-row");
            readWriteWarningBox.style.display = DisplayStyle.None;
            readWriteWarningBox.style.marginTop = 4;

            var warnLabel = new Label("⚠️ Texture is not Read/Write enabled.");
            warnLabel.style.color = new Color(1f, 0.75f, 0.2f);
            warnLabel.style.flexGrow = 1;
            warnLabel.style.fontSize = 11;
            readWriteWarningBox.Add(warnLabel);

            var fixReadWriteBtn = new RexButton("Enable Read/Write");
            fixReadWriteBtn.OnClick += EnableReadWriteOnTarget;
            readWriteWarningBox.Add(fixReadWriteBtn);
            texBox.Add(readWriteWarningBox);

            scrollView.Add(texBox);

            // --- 2. GRID CONFIGURATION SECTION ---
            var gridBox = new VisualElement();
            gridBox.AddToClassList("rex-box");

            var gridTitle = new Label("GRID SEGMENTATION");
            gridTitle.AddToClassList("rex-section-label");
            gridBox.Add(gridTitle);

            var gridRow1 = new VisualElement();
            gridRow1.AddToClassList("rex-row-cols-2");

            gridColsField = new IntegerField("Columns") { value = paletteData.GridColumns };
            gridColsField.AddToClassList("rex-col-left");
            gridColsField.RegisterValueChangedCallback(e => paletteData.GridColumns = Mathf.Max(1, e.newValue));
            gridRow1.Add(gridColsField);

            gridRowsField = new IntegerField("Rows") { value = paletteData.GridRows };
            gridRowsField.AddToClassList("rex-col-right");
            gridRowsField.RegisterValueChangedCallback(e => paletteData.GridRows = Mathf.Max(1, e.newValue));
            gridRow1.Add(gridRowsField);

            gridBox.Add(gridRow1);

            // Preset Resolution Buttons
            var presetRow = new VisualElement();
            presetRow.AddToClassList("rex-row");
            presetRow.style.marginTop = 4;

            var presetLabel = new Label("Presets:");
            presetLabel.style.fontSize = 11;
            presetLabel.style.marginRight = 6;
            presetRow.Add(presetLabel);

            int[] presetSizes = { 4, 8, 16, 32, 64 };
            foreach (var size in presetSizes)
            {
                int sz = size;
                var btn = new RexButton($"{sz}x{sz}");
                btn.AddToClassList("palette-preset-btn");
                btn.OnClick += () =>
                {
                    gridColsField.value = sz;
                    gridRowsField.value = sz;
                    InitGrid(sz, sz);
                };
                presetRow.Add(btn);
            }

            var autoDetectBtn = new RexButton("Auto Detect");
            autoDetectBtn.AddToClassList("palette-preset-btn");
            autoDetectBtn.tooltip = "Auto-detect color grid resolution from texture";
            autoDetectBtn.OnClick += AutoDetectGrid;
            presetRow.Add(autoDetectBtn);

            gridBox.Add(presetRow);

            var initGridBtn = new RexButton("INITIALIZE GRID FROM TEXTURE");
            initGridBtn.style.marginTop = 6;
            initGridBtn.OnClick += () => InitGrid(gridColsField.value, gridRowsField.value);
            gridBox.Add(initGridBtn);

            scrollView.Add(gridBox);

            // --- 3. PALETTE CANVAS SECTION ---
            var canvasBox = new VisualElement();
            canvasBox.AddToClassList("rex-box");

            var canvasHeaderRow = new VisualElement();
            canvasHeaderRow.AddToClassList("rex-row");
            canvasHeaderRow.style.justifyContent = Justify.SpaceBetween;

            var canvasTitle = new Label("PALETTE CANVAS");
            canvasTitle.AddToClassList("rex-section-label");
            canvasHeaderRow.Add(canvasTitle);

            gridLinesToggle = new Toggle("Grid Lines") { value = true };
            gridLinesToggle.RegisterValueChangedCallback(e =>
            {
                if (canvasElement != null) canvasElement.DrawGridLines = e.newValue;
            });
            canvasHeaderRow.Add(gridLinesToggle);
            canvasBox.Add(canvasHeaderRow);

            var canvasContainer = new VisualElement();
            canvasContainer.AddToClassList("palette-canvas-container");

            canvasElement = new PaletteCanvasElement();
            canvasElement.AddToClassList("palette-canvas-element");
            canvasElement.Data = paletteData;
            canvasElement.OnSelectionChanged += UpdateCellEditPanel;
            canvasElement.OnEyedropperColorSampled += OnColorPickedViaEyedropper;
            canvasContainer.Add(canvasElement);

            canvasBox.Add(canvasContainer);
            scrollView.Add(canvasBox);

            // --- 4. CELL SELECTION & COLOR EDITING SECTION ---
            cellEditContainer = new VisualElement();
            cellEditContainer.AddToClassList("rex-box");

            var cellTitle = new Label("CELL EDITING");
            cellTitle.AddToClassList("rex-section-label");
            cellEditContainer.Add(cellTitle);

            selectionInfoLabel = new Label("No cell selected");
            selectionInfoLabel.style.fontSize = 11;
            selectionInfoLabel.style.marginBottom = 4;
            cellEditContainer.Add(selectionInfoLabel);

            var colorEditRow = new VisualElement();
            colorEditRow.AddToClassList("rex-row-cols-2");

            colorPickerField = new ColorField("Color");
            colorPickerField.AddToClassList("rex-col-left");
            colorPickerField.RegisterValueChangedCallback(OnColorPickerChanged);
            colorEditRow.Add(colorPickerField);

            hexColorField = new TextField("Hex");
            hexColorField.AddToClassList("rex-col-right");
            hexColorField.RegisterValueChangedCallback(OnHexColorChanged);
            colorEditRow.Add(hexColorField);

            cellEditContainer.Add(colorEditRow);

            // Merge & Split buttons row
            var cellOpsRow = new VisualElement();
            cellOpsRow.AddToClassList("rex-row");
            cellOpsRow.style.marginTop = 6;

            mergeBtn = new RexButton("Merge Selected");
            mergeBtn.tooltip = "Combine selected cells into a single cell";
            mergeBtn.OnClick += MergeSelectedCells;
            cellOpsRow.Add(mergeBtn);

            splitBtn = new RexButton("Split Cell");
            splitBtn.tooltip = "Subdivide a merged cell back into individual 1x1 cells";
            splitBtn.OnClick += SplitSelectedCell;
            cellOpsRow.Add(splitBtn);

            var clearSelBtn = new RexButton("Deselect All");
            clearSelBtn.OnClick += () => canvasElement.ClearSelection();
            cellOpsRow.Add(clearSelBtn);

            cellEditContainer.Add(cellOpsRow);
            scrollView.Add(cellEditContainer);

            // --- 5. FIXED BOTTOM ACTIONS ---
            var bottomActionsContainer = new VisualElement();
            bottomActionsContainer.style.flexDirection = FlexDirection.Row;
            bottomActionsContainer.style.marginTop = 4;

            saveButton = new RexActionButton("SAVE & OVERWRITE", tint: new Color(0.2f, 0.5f, 1f));
            saveButton.style.flexGrow = 1;
            saveButton.style.marginRight = 4;
            saveButton.OnClick += SaveAndOverwriteTexture;
            bottomActionsContainer.Add(saveButton);

            var saveCopyBtn = new RexButton("Save As Copy...");
            saveCopyBtn.style.height = 42;
            saveCopyBtn.OnClick += SaveTextureAsCopy;
            bottomActionsContainer.Add(saveCopyBtn);

            root.Add(bottomActionsContainer);

            UpdateCellEditPanel();
        }

        private void OnTextureAssigned(Texture2D tex)
        {
            targetTexture = tex;
            if (tex != null)
            {
                string path = AssetDatabase.GetAssetPath(tex);
                bool isReadable = IsTextureReadable(tex);

                textureInfoLabel.text = $"Dimensions: {tex.width}x{tex.height} | Format: {tex.format} | Path: {path}";
                readWriteWarningBox.style.display = isReadable ? DisplayStyle.None : DisplayStyle.Flex;

                // Auto initialize grid matching texture or default
                if (isReadable)
                {
                    AutoDetectGrid();
                }
            }
            else
            {
                textureInfoLabel.text = "No texture assigned";
                readWriteWarningBox.style.display = DisplayStyle.None;
            }
        }

        private bool IsTextureReadable(Texture2D tex)
        {
            if (tex == null) return false;
            try
            {
                tex.GetPixel(0, 0);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private void EnableReadWriteOnTarget()
        {
            if (targetTexture == null) return;
            string path = AssetDatabase.GetAssetPath(targetTexture);
            if (string.IsNullOrEmpty(path)) return;

            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer != null)
            {
                importer.isReadable = true;
                importer.SaveAndReimport();
                OnTextureAssigned(targetTexture);
            }
        }

        private void InitGrid(int cols, int rows)
        {
            Undo.RecordObject(paletteData, "Initialize Palette Grid");
            paletteData.InitializeGrid(cols, rows, targetTexture);
            if (canvasElement != null)
            {
                canvasElement.ClearSelection();
                canvasElement.MarkDirtyRepaint();
            }
        }

        private void AutoDetectGrid()
        {
            Undo.RecordObject(paletteData, "Auto-Detect Palette Grid");
            paletteData.AutoDetectFromTexture(targetTexture, gridColsField.value, gridRowsField.value);
            gridColsField.SetValueWithoutNotify(paletteData.GridColumns);
            gridRowsField.SetValueWithoutNotify(paletteData.GridRows);

            if (canvasElement != null)
            {
                canvasElement.ClearSelection();
                canvasElement.MarkDirtyRepaint();
            }
        }

        private void UpdateCellEditPanel()
        {
            if (canvasElement == null) return;

            var selectedCells = canvasElement.GetSelectedCells();
            int count = selectedCells.Count;

            if (count == 0)
            {
                selectionInfoLabel.text = "No cell selected. Click or drag on canvas to select.";
                colorPickerField.SetEnabled(false);
                hexColorField.SetEnabled(false);
                mergeBtn.SetEnabled(false);
                splitBtn.SetEnabled(false);
            }
            else if (count == 1)
            {
                var cell = selectedCells[0];
                selectionInfoLabel.text = $"Selected Cell: Col {cell.gridRect.x}, Row {cell.gridRect.y} (Span: {cell.gridRect.width}x{cell.gridRect.height})";

                colorPickerField.SetEnabled(true);
                hexColorField.SetEnabled(true);

                colorPickerField.SetValueWithoutNotify(cell.color);
                hexColorField.SetValueWithoutNotify("#" + ColorUtility.ToHtmlStringRGBA(cell.color));

                mergeBtn.SetEnabled(false);
                splitBtn.SetEnabled(cell.gridRect.width > 1 || cell.gridRect.height > 1);
            }
            else
            {
                selectionInfoLabel.text = $"Selected: {count} Cells";

                colorPickerField.SetEnabled(true);
                hexColorField.SetEnabled(true);

                // Use first cell's color as representation
                colorPickerField.SetValueWithoutNotify(selectedCells[0].color);
                hexColorField.SetValueWithoutNotify("#" + ColorUtility.ToHtmlStringRGBA(selectedCells[0].color));

                mergeBtn.SetEnabled(true);
                splitBtn.SetEnabled(false);
            }
        }

        private void OnColorPickerChanged(ChangeEvent<Color> evt)
        {
            if (canvasElement == null || paletteData == null) return;

            var selectedCells = canvasElement.GetSelectedCells();
            if (selectedCells.Count == 0) return;

            Undo.RecordObject(paletteData, "Change Cell Color");
            foreach (var cell in selectedCells)
            {
                cell.color = evt.newValue;
            }

            hexColorField.SetValueWithoutNotify("#" + ColorUtility.ToHtmlStringRGBA(evt.newValue));
            canvasElement.MarkDirtyRepaint();
        }

        private void OnHexColorChanged(ChangeEvent<string> evt)
        {
            if (canvasElement == null || paletteData == null) return;
            string hex = evt.newValue;
            if (!hex.StartsWith("#")) hex = "#" + hex;

            if (ColorUtility.TryParseHtmlString(hex, out Color parsedColor))
            {
                var selectedCells = canvasElement.GetSelectedCells();
                if (selectedCells.Count == 0) return;

                Undo.RecordObject(paletteData, "Change Cell Hex Color");
                foreach (var cell in selectedCells)
                {
                    cell.color = parsedColor;
                }

                colorPickerField.SetValueWithoutNotify(parsedColor);
                canvasElement.MarkDirtyRepaint();
            }
        }

        private void OnColorPickedViaEyedropper(Color sampledColor)
        {
            var selectedCells = canvasElement.GetSelectedCells();
            if (selectedCells.Count == 0) return;

            Undo.RecordObject(paletteData, "Eyedropper Sample Color");
            foreach (var cell in selectedCells)
            {
                cell.color = sampledColor;
            }

            colorPickerField.SetValueWithoutNotify(sampledColor);
            hexColorField.SetValueWithoutNotify("#" + ColorUtility.ToHtmlStringRGBA(sampledColor));
            canvasElement.MarkDirtyRepaint();
        }

        private void MergeSelectedCells()
        {
            if (canvasElement == null || paletteData == null) return;

            var selectedCells = canvasElement.GetSelectedCells();
            if (selectedCells.Count < 2) return;

            Undo.RecordObject(paletteData, "Merge Palette Cells");
            if (paletteData.MergeCells(selectedCells, out var mergedCell))
            {
                canvasElement.SetSelectedCells(new[] { mergedCell });
                canvasElement.MarkDirtyRepaint();
                UpdateCellEditPanel();
            }
        }

        private void SplitSelectedCell()
        {
            if (canvasElement == null || paletteData == null) return;

            var selectedCells = canvasElement.GetSelectedCells();
            if (selectedCells.Count != 1) return;

            Undo.RecordObject(paletteData, "Split Palette Cell");
            if (paletteData.SplitCell(selectedCells[0], out var newCells))
            {
                canvasElement.SetSelectedCells(newCells);
                canvasElement.MarkDirtyRepaint();
                UpdateCellEditPanel();
            }
        }

        private void SaveAndOverwriteTexture()
        {
            if (targetTexture == null)
            {
                EditorUtility.DisplayDialog("Save Palette", "Please assign a target Palette Texture first.", "OK");
                return;
            }

            string assetPath = AssetDatabase.GetAssetPath(targetTexture);
            if (string.IsNullOrEmpty(assetPath))
            {
                EditorUtility.DisplayDialog("Save Palette", "Target texture is not an asset saved on disk.", "OK");
                return;
            }

            string fullPath = Path.Combine(Directory.GetCurrentDirectory(), assetPath);

            int exportW = targetTexture.width > 0 ? targetTexture.width : paletteData.GridColumns * 8;
            int exportH = targetTexture.height > 0 ? targetTexture.height : paletteData.GridRows * 8;

            Texture2D renderedTex = paletteData.RenderToTexture(exportW, exportH);
            byte[] pngBytes = renderedTex.EncodeToPNG();
            DestroyImmediate(renderedTex);

            File.WriteAllBytes(fullPath, pngBytes);
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
        }

        private void SaveTextureAsCopy()
        {
            string defaultName = targetTexture != null ? targetTexture.name + "_edited.png" : "palette_edited.png";
            string path = EditorUtility.SaveFilePanelInProject("Save Palette Texture Copy", defaultName, "png", "Save edited palette texture as PNG copy");

            if (string.IsNullOrEmpty(path)) return;

            int exportW = targetTexture != null ? targetTexture.width : paletteData.GridColumns * 8;
            int exportH = targetTexture != null ? targetTexture.height : paletteData.GridRows * 8;

            Texture2D renderedTex = paletteData.RenderToTexture(exportW, exportH);
            byte[] pngBytes = renderedTex.EncodeToPNG();
            DestroyImmediate(renderedTex);

            string fullPath = Path.Combine(Directory.GetCurrentDirectory(), path);
            File.WriteAllBytes(fullPath, pngBytes);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);

            var newTex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (newTex != null)
            {
                textureField.Value = newTex;
            }
        }
    }
}
