using System;
using System.Collections.Generic;
using UnityEngine;

namespace RexTools.PaletteTextureModifier.Editor
{
    [Serializable]
    public class PaletteCell
    {
        public string id = Guid.NewGuid().ToString();
        public RectInt gridRect = new RectInt(0, 0, 1, 1);
        public Color color = Color.white;

        public PaletteCell Clone()
        {
            return new PaletteCell
            {
                id = this.id,
                gridRect = this.gridRect,
                color = this.color
            };
        }
    }

    public class PaletteData : ScriptableObject
    {
        [SerializeField] private int gridColumns = 8;
        [SerializeField] private int gridRows = 8;
        [SerializeField] private List<PaletteCell> cells = new List<PaletteCell>();

        public int GridColumns
        {
            get => gridColumns;
            set => gridColumns = Mathf.Max(1, value);
        }

        public int GridRows
        {
            get => gridRows;
            set => gridRows = Mathf.Max(1, value);
        }

        public IReadOnlyList<PaletteCell> Cells => cells;

        public void Clear()
        {
            cells.Clear();
        }

        public void InitializeGrid(int columns, int rows, Texture2D sourceTex = null)
        {
            gridColumns = Mathf.Max(1, columns);
            gridRows = Mathf.Max(1, rows);
            cells.Clear();

            for (int r = 0; r < gridRows; r++)
            {
                for (int c = 0; c < gridColumns; c++)
                {
                    Color cellColor = Color.white;
                    if (sourceTex != null)
                    {
                        cellColor = SampleAverageColor(sourceTex, c, r, 1, 1, gridColumns, gridRows);
                    }

                    var cell = new PaletteCell
                    {
                        id = Guid.NewGuid().ToString(),
                        gridRect = new RectInt(c, r, 1, 1),
                        color = cellColor
                    };
                    cells.Add(cell);
                }
            }
        }

        public PaletteCell GetCellAt(int col, int row)
        {
            foreach (var cell in cells)
            {
                if (col >= cell.gridRect.x && col < cell.gridRect.x + cell.gridRect.width &&
                    row >= cell.gridRect.y && row < cell.gridRect.y + cell.gridRect.height)
                {
                    return cell;
                }
            }
            return null;
        }

        public List<PaletteCell> GetCellsInRect(RectInt area)
        {
            var result = new List<PaletteCell>();
            foreach (var cell in cells)
            {
                if (cell.gridRect.Overlaps(area))
                {
                    result.Add(cell);
                }
            }
            return result;
        }

        public bool MergeCells(List<PaletteCell> cellsToMerge, out PaletteCell mergedCell)
        {
            mergedCell = null;
            if (cellsToMerge == null || cellsToMerge.Count < 2) return false;

            int minX = int.MaxValue, minY = int.MaxValue;
            int maxX = int.MinValue, maxY = int.MinValue;
            Color avgColor = Color.clear;

            foreach (var c in cellsToMerge)
            {
                if (!cells.Contains(c)) continue;
                minX = Mathf.Min(minX, c.gridRect.x);
                minY = Mathf.Min(minY, c.gridRect.y);
                maxX = Mathf.Max(maxX, c.gridRect.x + c.gridRect.width);
                maxY = Mathf.Max(maxY, c.gridRect.y + c.gridRect.height);
                avgColor += c.color;
            }

            avgColor /= cellsToMerge.Count;
            var mergedRect = new RectInt(minX, minY, maxX - minX, maxY - minY);

            var overlapped = GetCellsInRect(mergedRect);
            foreach (var c in overlapped)
            {
                cells.Remove(c);
            }

            mergedCell = new PaletteCell
            {
                id = Guid.NewGuid().ToString(),
                gridRect = mergedRect,
                color = avgColor
            };

            cells.Add(mergedCell);
            return true;
        }

        public bool SplitCell(PaletteCell cellToSplit, out List<PaletteCell> newCells)
        {
            newCells = new List<PaletteCell>();
            if (cellToSplit == null || !cells.Contains(cellToSplit)) return false;
            if (cellToSplit.gridRect.width <= 1 && cellToSplit.gridRect.height <= 1) return false;

            int startX = cellToSplit.gridRect.x;
            int startY = cellToSplit.gridRect.y;
            int w = cellToSplit.gridRect.width;
            int h = cellToSplit.gridRect.height;
            Color col = cellToSplit.color;

            cells.Remove(cellToSplit);

            for (int r = startY; r < startY + h; r++)
            {
                for (int c = startX; c < startX + w; c++)
                {
                    var newCell = new PaletteCell
                    {
                        id = Guid.NewGuid().ToString(),
                        gridRect = new RectInt(c, r, 1, 1),
                        color = col
                    };
                    cells.Add(newCell);
                    newCells.Add(newCell);
                }
            }
            return true;
        }

        public void AutoDetectFromTexture(Texture2D tex, int targetCols = 8, int targetRows = 8)
        {
            if (tex == null)
            {
                InitializeGrid(targetCols, targetRows);
                return;
            }

            int bestCols = targetCols;
            int bestRows = targetRows;

            if (tex.width <= 128 && tex.height <= 128)
            {
                int minBlockW = FindMinColorBlockSizeHorizontal(tex);
                int minBlockH = FindMinColorBlockSizeVertical(tex);

                if (minBlockW > 0 && tex.width % minBlockW == 0)
                {
                    bestCols = tex.width / minBlockW;
                }
                if (minBlockH > 0 && tex.height % minBlockH == 0)
                {
                    bestRows = tex.height / minBlockH;
                }
            }

            InitializeGrid(bestCols, bestRows, tex);
        }

        public Texture2D RenderToTexture(int width, int height)
        {
            var tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Point;
            tex.wrapMode = TextureWrapMode.Clamp;

            Color[] pixels = new Color[width * height];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = Color.clear;

            foreach (var cell in cells)
            {
                float startXNorm = (float)cell.gridRect.x / gridColumns;
                float startYNorm = (float)cell.gridRect.y / gridRows;
                float endXNorm = (float)(cell.gridRect.x + cell.gridRect.width) / gridColumns;
                float endYNorm = (float)(cell.gridRect.y + cell.gridRect.height) / gridRows;

                int pxStart = Mathf.Clamp(Mathf.FloorToInt(startXNorm * width), 0, width - 1);
                int pxEnd = Mathf.Clamp(Mathf.CeilToInt(endXNorm * width), pxStart + 1, width);
                int pyStart = Mathf.Clamp(Mathf.FloorToInt(startYNorm * height), 0, height - 1);
                int pyEnd = Mathf.Clamp(Mathf.CeilToInt(endYNorm * height), pyStart + 1, height);

                for (int y = pyStart; y < pyEnd; y++)
                {
                    for (int x = pxStart; x < pxEnd; x++)
                    {
                        pixels[y * width + x] = cell.color;
                    }
                }
            }

            tex.SetPixels(pixels);
            tex.Apply();
            return tex;
        }

        private Color SampleAverageColor(Texture2D tex, int col, int row, int colSpan, int rowSpan, int totalCols, int totalRows)
        {
            float startXNorm = (float)col / totalCols;
            float startYNorm = (float)row / totalRows;
            float endXNorm = (float)(col + colSpan) / totalCols;
            float endYNorm = (float)(row + rowSpan) / totalRows;

            int pxStart = Mathf.Clamp(Mathf.FloorToInt(startXNorm * tex.width), 0, tex.width - 1);
            int pxEnd = Mathf.Clamp(Mathf.CeilToInt(endXNorm * tex.width), pxStart + 1, tex.width);
            int pyStart = Mathf.Clamp(Mathf.FloorToInt(startYNorm * tex.height), 0, tex.height - 1);
            int pyEnd = Mathf.Clamp(Mathf.CeilToInt(endYNorm * tex.height), pyStart + 1, tex.height);

            Color sum = Color.clear;
            int count = 0;

            for (int y = pyStart; y < pyEnd; y++)
            {
                for (int x = pxStart; x < pxEnd; x++)
                {
                    sum += tex.GetPixel(x, y);
                    count++;
                }
            }

            return count > 0 ? sum / count : Color.white;
        }

        private int FindMinColorBlockSizeHorizontal(Texture2D tex)
        {
            int currentRun = 1;
            int minRun = tex.width;
            int row = tex.height / 2;

            Color prev = tex.GetPixel(0, row);
            for (int x = 1; x < tex.width; x++)
            {
                Color c = tex.GetPixel(x, row);
                if (ColorsMatch(c, prev))
                {
                    currentRun++;
                }
                else
                {
                    if (currentRun > 0 && currentRun < minRun) minRun = currentRun;
                    currentRun = 1;
                    prev = c;
                }
            }
            if (currentRun > 0 && currentRun < minRun) minRun = currentRun;
            return minRun;
        }

        private int FindMinColorBlockSizeVertical(Texture2D tex)
        {
            int currentRun = 1;
            int minRun = tex.height;
            int col = tex.width / 2;

            Color prev = tex.GetPixel(col, 0);
            for (int y = 1; y < tex.height; y++)
            {
                Color c = tex.GetPixel(col, y);
                if (ColorsMatch(c, prev))
                {
                    currentRun++;
                }
                else
                {
                    if (currentRun > 0 && currentRun < minRun) minRun = currentRun;
                    currentRun = 1;
                    prev = c;
                }
            }
            if (currentRun > 0 && currentRun < minRun) minRun = currentRun;
            return minRun;
        }

        private bool ColorsMatch(Color a, Color b)
        {
            return Mathf.Abs(a.r - b.r) < 0.02f &&
                   Mathf.Abs(a.g - b.g) < 0.02f &&
                   Mathf.Abs(a.b - b.b) < 0.02f &&
                   Mathf.Abs(a.a - b.a) < 0.02f;
        }
    }
}
