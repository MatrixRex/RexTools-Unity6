using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace RexTools.PaletteTextureModifier.Editor
{
    public class PaletteCanvasElement : VisualElement
    {
        private PaletteData paletteData;
        private HashSet<string> selectedCellIds = new HashSet<string>();
        private PaletteCell hoveredCell;
        private bool isDragSelecting = false;
        private Vector2 dragStartPos;
        private Vector2 dragCurrentPos;
        private bool drawGridLines = true;

        public Action OnSelectionChanged;
        public Action<Color> OnEyedropperColorSampled;

        public PaletteData Data
        {
            get => paletteData;
            set
            {
                paletteData = value;
                selectedCellIds.Clear();
                hoveredCell = null;
                MarkDirtyRepaint();
            }
        }

        public HashSet<string> SelectedCellIds => selectedCellIds;

        public bool DrawGridLines
        {
            get => drawGridLines;
            set
            {
                drawGridLines = value;
                MarkDirtyRepaint();
            }
        }

        public PaletteCanvasElement()
        {
            style.flexGrow = 1;
            style.minHeight = 220;
            style.backgroundColor = new Color(0.12f, 0.12f, 0.12f, 1f);
            style.borderTopWidth = 1;
            style.borderBottomWidth = 1;
            style.borderLeftWidth = 1;
            style.borderRightWidth = 1;
            style.borderTopColor = new Color(0.2f, 0.2f, 0.2f, 1f);
            style.borderBottomColor = new Color(0.2f, 0.2f, 0.2f, 1f);
            style.borderLeftColor = new Color(0.2f, 0.2f, 0.2f, 1f);
            style.borderRightColor = new Color(0.2f, 0.2f, 0.2f, 1f);
            style.borderTopLeftRadius = 4;
            style.borderTopRightRadius = 4;
            style.borderBottomLeftRadius = 4;
            style.borderBottomRightRadius = 4;

            generateVisualContent += OnGenerateVisualContent;

            RegisterCallback<MouseDownEvent>(OnMouseDown);
            RegisterCallback<MouseMoveEvent>(OnMouseMove);
            RegisterCallback<MouseUpEvent>(OnMouseUp);
            RegisterCallback<MouseLeaveEvent>(OnMouseLeave);
        }

        public List<PaletteCell> GetSelectedCells()
        {
            var list = new List<PaletteCell>();
            if (paletteData == null) return list;
            foreach (var cell in paletteData.Cells)
            {
                if (selectedCellIds.Contains(cell.id))
                {
                    list.Add(cell);
                }
            }
            return list;
        }

        public void SetSelectedCells(IEnumerable<PaletteCell> cells)
        {
            selectedCellIds.Clear();
            if (cells != null)
            {
                foreach (var c in cells)
                {
                    if (c != null) selectedCellIds.Add(c.id);
                }
            }
            MarkDirtyRepaint();
            OnSelectionChanged?.Invoke();
        }

        public void ClearSelection()
        {
            selectedCellIds.Clear();
            MarkDirtyRepaint();
            OnSelectionChanged?.Invoke();
        }

        private void OnGenerateVisualContent(MeshGenerationContext mgc)
        {
            var painter = mgc.painter2D;
            float width = contentRect.width;
            float height = contentRect.height;

            if (width <= 0 || height <= 0) return;

            // Draw checkerboard transparency grid
            DrawCheckerboard(painter, width, height, 16);

            if (paletteData == null || paletteData.Cells.Count == 0) return;

            int totalCols = paletteData.GridColumns;
            int totalRows = paletteData.GridRows;

            float cellW = width / totalCols;
            float cellH = height / totalRows;

            // 1. Draw Cell Color Fills
            foreach (var cell in paletteData.Cells)
            {
                Rect cellScreenRect = GetCellScreenRect(cell, width, height, totalCols, totalRows);

                painter.BeginPath();
                DrawRectPath(painter, cellScreenRect);
                painter.fillColor = cell.color;
                painter.Fill();
            }

            // 2. Draw Grid Lines
            if (drawGridLines)
            {
                painter.strokeColor = new Color(0f, 0f, 0f, 0.35f);
                painter.lineWidth = 1f;

                for (int c = 1; c < totalCols; c++)
                {
                    float x = c * cellW;
                    painter.BeginPath();
                    painter.MoveTo(new Vector2(x, 0));
                    painter.LineTo(new Vector2(x, height));
                    painter.Stroke();
                }

                for (int r = 1; r < totalRows; r++)
                {
                    float y = r * cellH;
                    painter.BeginPath();
                    painter.MoveTo(new Vector2(0, y));
                    painter.LineTo(new Vector2(width, y));
                    painter.Stroke();
                }
            }

            // 3. Draw Cell Borders (for merged boundaries or cell outlines)
            foreach (var cell in paletteData.Cells)
            {
                Rect cellScreenRect = GetCellScreenRect(cell, width, height, totalCols, totalRows);
                painter.BeginPath();
                DrawRectPath(painter, cellScreenRect);
                painter.strokeColor = new Color(0.15f, 0.15f, 0.15f, 0.5f);
                painter.lineWidth = 1f;
                painter.Stroke();
            }

            // 4. Draw Hovered Cell Outline
            if (hoveredCell != null && !selectedCellIds.Contains(hoveredCell.id))
            {
                Rect hRect = GetCellScreenRect(hoveredCell, width, height, totalCols, totalRows);
                painter.BeginPath();
                DrawRectPath(painter, hRect);
                painter.strokeColor = new Color(1f, 1f, 1f, 0.8f);
                painter.lineWidth = 2f;
                painter.Stroke();
            }

            // 5. Draw Selected Cell Outlines & Highlights
            foreach (var cell in paletteData.Cells)
            {
                if (selectedCellIds.Contains(cell.id))
                {
                    Rect sRect = GetCellScreenRect(cell, width, height, totalCols, totalRows);

                    // Cyan selection border
                    painter.BeginPath();
                    DrawRectPath(painter, sRect);
                    painter.strokeColor = new Color(0.2f, 0.8f, 1f, 1f);
                    painter.lineWidth = 2.5f;
                    painter.Stroke();
                }
            }

            // 6. Draw Drag Selection Box
            if (isDragSelecting)
            {
                float xMin = Mathf.Min(dragStartPos.x, dragCurrentPos.x);
                float yMin = Mathf.Min(dragStartPos.y, dragCurrentPos.y);
                float boxW = Mathf.Abs(dragCurrentPos.x - dragStartPos.x);
                float boxH = Mathf.Abs(dragCurrentPos.y - dragStartPos.y);
                Rect dragRect = new Rect(xMin, yMin, boxW, boxH);

                painter.BeginPath();
                DrawRectPath(painter, dragRect);
                painter.fillColor = new Color(0.2f, 0.7f, 1f, 0.2f);
                painter.Fill();

                painter.BeginPath();
                DrawRectPath(painter, dragRect);
                painter.strokeColor = new Color(0.3f, 0.8f, 1f, 0.9f);
                painter.lineWidth = 1.5f;
                painter.Stroke();
            }
        }

        private void DrawRectPath(Painter2D painter, Rect rect)
        {
            painter.MoveTo(new Vector2(rect.xMin, rect.yMin));
            painter.LineTo(new Vector2(rect.xMax, rect.yMin));
            painter.LineTo(new Vector2(rect.xMax, rect.yMax));
            painter.LineTo(new Vector2(rect.xMin, rect.yMax));
            painter.ClosePath();
        }

        private void DrawCheckerboard(Painter2D painter, float width, float height, float tileSize)
        {
            int cols = Mathf.CeilToInt(width / tileSize);
            int rows = Mathf.CeilToInt(height / tileSize);

            Color c1 = new Color(0.22f, 0.22f, 0.22f, 1f);
            Color c2 = new Color(0.18f, 0.18f, 0.18f, 1f);

            for (int r = 0; r < rows; r++)
            {
                for (int c = 0; c < cols; c++)
                {
                    Rect tile = new Rect(c * tileSize, r * tileSize, tileSize, tileSize);
                    painter.BeginPath();
                    DrawRectPath(painter, tile);
                    painter.fillColor = (c + r) % 2 == 0 ? c1 : c2;
                    painter.Fill();
                }
            }
        }

        private Rect GetCellScreenRect(PaletteCell cell, float canvasW, float canvasH, int totalCols, int totalRows)
        {
            float cellW = canvasW / totalCols;
            float cellH = canvasH / totalRows;

            return new Rect(
                cell.gridRect.x * cellW,
                cell.gridRect.y * cellH,
                cell.gridRect.width * cellW,
                cell.gridRect.height * cellH
            );
        }

        private void OnMouseDown(MouseDownEvent evt)
        {
            if (paletteData == null || evt.button != 0) return;

            Vector2 localPos = evt.localMousePosition;
            dragStartPos = localPos;
            dragCurrentPos = localPos;
            isDragSelecting = true;

            this.CaptureMouse();

            PaletteCell clickedCell = GetCellAtPosition(localPos);
            if (evt.altKey && clickedCell != null)
            {
                // Eyedropper sampling
                OnEyedropperColorSampled?.Invoke(clickedCell.color);
                isDragSelecting = false;
                this.ReleaseMouse();
                return;
            }

            if (!evt.shiftKey && !evt.actionKey)
            {
                selectedCellIds.Clear();
            }

            if (clickedCell != null)
            {
                if (selectedCellIds.Contains(clickedCell.id) && (evt.shiftKey || evt.actionKey))
                {
                    selectedCellIds.Remove(clickedCell.id);
                }
                else
                {
                    selectedCellIds.Add(clickedCell.id);
                }
            }

            MarkDirtyRepaint();
            OnSelectionChanged?.Invoke();
        }

        private void OnMouseMove(MouseMoveEvent evt)
        {
            if (paletteData == null) return;

            Vector2 localPos = evt.localMousePosition;
            PaletteCell cell = GetCellAtPosition(localPos);

            if (hoveredCell != cell)
            {
                hoveredCell = cell;
                MarkDirtyRepaint();
            }

            if (isDragSelecting)
            {
                dragCurrentPos = localPos;
                SelectCellsInDragBox(evt.shiftKey || evt.actionKey);
                MarkDirtyRepaint();
            }
        }

        private void OnMouseUp(MouseUpEvent evt)
        {
            if (isDragSelecting)
            {
                isDragSelecting = false;
                this.ReleaseMouse();
                MarkDirtyRepaint();
                OnSelectionChanged?.Invoke();
            }
        }

        private void OnMouseLeave(MouseLeaveEvent evt)
        {
            if (hoveredCell != null)
            {
                hoveredCell = null;
                MarkDirtyRepaint();
            }
        }

        private PaletteCell GetCellAtPosition(Vector2 localPos)
        {
            if (paletteData == null || contentRect.width <= 0 || contentRect.height <= 0) return null;

            int col = Mathf.FloorToInt((localPos.x / contentRect.width) * paletteData.GridColumns);
            int row = Mathf.FloorToInt((localPos.y / contentRect.height) * paletteData.GridRows);

            col = Mathf.Clamp(col, 0, paletteData.GridColumns - 1);
            row = Mathf.Clamp(row, 0, paletteData.GridRows - 1);

            return paletteData.GetCellAt(col, row);
        }

        private void SelectCellsInDragBox(bool isMultiSelect)
        {
            if (paletteData == null) return;

            float xMin = Mathf.Min(dragStartPos.x, dragCurrentPos.x);
            float yMin = Mathf.Min(dragStartPos.y, dragCurrentPos.y);
            float xMax = Mathf.Max(dragStartPos.x, dragCurrentPos.x);
            float yMax = Mathf.Max(dragStartPos.y, dragCurrentPos.y);

            int colStart = Mathf.Clamp(Mathf.FloorToInt((xMin / contentRect.width) * paletteData.GridColumns), 0, paletteData.GridColumns - 1);
            int colEnd = Mathf.Clamp(Mathf.FloorToInt((xMax / contentRect.width) * paletteData.GridColumns), 0, paletteData.GridColumns - 1);
            int rowStart = Mathf.Clamp(Mathf.FloorToInt((yMin / contentRect.height) * paletteData.GridRows), 0, paletteData.GridRows - 1);
            int rowEnd = Mathf.Clamp(Mathf.FloorToInt((yMax / contentRect.height) * paletteData.GridRows), 0, paletteData.GridRows - 1);

            if (!isMultiSelect)
            {
                selectedCellIds.Clear();
            }

            for (int r = rowStart; r <= rowEnd; r++)
            {
                for (int c = colStart; c <= colEnd; c++)
                {
                    var cell = paletteData.GetCellAt(c, r);
                    if (cell != null)
                    {
                        selectedCellIds.Add(cell.id);
                    }
                }
            }
        }
    }
}
