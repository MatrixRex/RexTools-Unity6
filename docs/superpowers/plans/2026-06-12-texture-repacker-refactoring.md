# Texture Repacker Refactoring Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Refactor the large single-file `TexturePackSeparator.cs` into modular, single-responsibility components separating Editor UI from core processing algorithms.

**Architecture:** Create dedicated C# classes for Packing, Unpacking, Mixing, and Utility operations, and move the Live Preview Window popup into its own file. The main Editor Window will act as the UI and controller layer, delegating operations to these engines.

**Tech Stack:** Unity 6 C# (UI Toolkit, EditorWindow, Texture2D/RenderTexture GPU APIs)

---

### Task 1: Create TextureRepackerUtils.cs

**Files:**
- Create: `p:/Personal/00 Unity/03 RexTools/RexTools/RexTools-Unity6/Editor/Texture Repacker/TextureRepackerUtils.cs`

- [ ] **Step 1: Write utility helper class**
  Create the file with the following complete code content:
  ```csharp
  using UnityEngine;
  using System.Collections.Generic;

  namespace RexTools.TextureRepacker.Editor
  {
      public static class TextureRepackerUtils
      {
          private static readonly Dictionary<int, Color[]> PixelCache = new Dictionary<int, Color[]>();

          public static void ClearCache()
          {
              PixelCache.Clear();
          }

          public static void RemoveFromCache(int instanceID)
          {
              PixelCache.Remove(instanceID);
          }

          public static Color[] GetReadablePixels(Texture2D tex)
          {
              if (tex == null) return new Color[0];

              int id = tex.GetInstanceID();
              if (PixelCache.TryGetValue(id, out var cached)) return cached;

              Color[] pixels;
              if (tex.isReadable)
              {
                  pixels = tex.GetPixels();
              }
              else
              {
                  var copy = MakeReadableCopy(tex);
                  pixels = copy.GetPixels();
                  Object.DestroyImmediate(copy);
              }
              PixelCache[id] = pixels;
              return pixels;
          }

          public static Texture2D MakeReadableCopy(Texture2D tex)
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

          public static string GenerateBaseName(string name)
          {
              string[] suffixes = { "_packed", "_pack", "_combined", "_tex", "_diffuse", "_albedo" };
              foreach (var s in suffixes)
              {
                  if (name.EndsWith(s, System.StringComparison.OrdinalIgnoreCase))
                      name = name.Substring(0, name.Length - s.Length);
              }
              return name.TrimEnd('_', '-', ' ');
          }
      }
  }
  ```

- [ ] **Step 2: Verify compilation**
  Wait for Unity to compile or build (if running compiler tools, otherwise compiling in Unity editor) and verify there are no syntax errors in `TextureRepackerUtils.cs`.

---

### Task 2: Create TextureMixer.cs

**Files:**
- Create: `p:/Personal/00 Unity/03 RexTools/RexTools/RexTools-Unity6/Editor/Texture Repacker/TextureMixer.cs`

- [ ] **Step 1: Write mixing engine class**
  Create the file with the following complete code content:
  ```csharp
  using UnityEngine;

  namespace RexTools.TextureRepacker.Editor
  {
      public enum BlendMode 
      { 
          Multiply, Add, Screen, Overlay, Subtract, Divide, Darken, Lighten, SoftLight, HardLight 
      }

      public static class TextureMixer
      {
          public static Texture2D Mix(
              Texture2D baseTex,
              Texture2D layerTex,
              int baseChannel,
              int layerChannel,
              BlendMode blendMode,
              float opacity,
              System.Action<string, float> progressCallback = null)
          {
              if (baseTex == null) return null;

              int w = baseTex.width;
              int h = baseTex.height;

              Texture2D result = new Texture2D(w, h, TextureFormat.RGBA32, false);
              Color[] pixels = new Color[w * h];

              Color[] basePixels = TextureRepackerUtils.GetReadablePixels(baseTex);
              Color[] layerPixels = layerTex != null ? TextureRepackerUtils.GetReadablePixels(layerTex) : null;
              int lW = layerTex != null ? layerTex.width : 1;
              int lH = layerTex != null ? layerTex.height : 1;

              int progressInterval = Mathf.Max(1, h / 20);
              for (int y = 0; y < h; y++)
              {
                  if (y % progressInterval == 0 && progressCallback != null)
                  {
                      progressCallback($"Row {y}/{h}", (float)y / h);
                  }

                  for (int x = 0; x < w; x++)
                  {
                      Color bc = basePixels[y * w + x];
                      Color lc = Color.black;
                      if (layerPixels != null)
                      {
                          int lx = Mathf.Clamp(x * lW / w, 0, lW - 1);
                          int ly = Mathf.Clamp(y * lH / h, 0, lH - 1);
                          lc = layerPixels[ly * lW + lx];
                      }
                      Color fb = ApplyChannelSelect(bc, baseChannel);
                      Color fl = ApplyChannelSelect(lc, layerChannel);
                      Color blended = BlendColors(fb, fl, blendMode);
                      pixels[y * w + x] = Color.Lerp(fb, blended, opacity);
                  }
              }

              result.SetPixels(pixels);
              result.Apply();
              return result;
          }

          public static Color ApplyChannelSelect(Color c, int channel)
          {
              if (channel < 0) return c; // Full
              float v = channel == 0 ? c.r : channel == 1 ? c.g : channel == 2 ? c.b : c.a;
              return new Color(v, v, v, 1f);
          }

          public static Color BlendColors(Color b, Color l, BlendMode mode)
          {
              Color result = b;
              switch (mode)
              {
                  case BlendMode.Multiply:  result = new Color(b.r * l.r, b.g * l.g, b.b * l.b, b.a * l.a); break;
                  case BlendMode.Add:       result = new Color(Mathf.Clamp01(b.r + l.r), Mathf.Clamp01(b.g + l.g), Mathf.Clamp01(b.b + l.b), Mathf.Clamp01(b.a + l.a)); break;
                  case BlendMode.Screen:    result = new Color(1 - (1 - b.r) * (1 - l.r), 1 - (1 - b.g) * (1 - l.g), 1 - (1 - b.b) * (1 - l.b), 1 - (1 - b.a) * (1 - l.a)); break;
                  case BlendMode.Subtract:  result = new Color(Mathf.Clamp01(b.r - l.r), Mathf.Clamp01(b.g - l.g), Mathf.Clamp01(b.b - l.b), Mathf.Clamp01(b.a - l.a)); break;
                  case BlendMode.Divide:    result = new Color(Mathf.Clamp01(l.r < 0.001f ? 1f : b.r / l.r), Mathf.Clamp01(l.g < 0.001f ? 1f : b.g / l.g), Mathf.Clamp01(l.b < 0.001f ? 1f : b.b / l.b), Mathf.Clamp01(l.a < 0.001f ? 1f : b.a / l.a)); break;
                  case BlendMode.Darken:    result = new Color(Mathf.Min(b.r, l.r), Mathf.Min(b.g, l.g), Mathf.Min(b.b, l.b), Mathf.Min(b.a, l.a)); break;
                  case BlendMode.Lighten:   result = new Color(Mathf.Max(b.r, l.r), Mathf.Max(b.g, l.g), Mathf.Max(b.b, l.b), Mathf.Max(b.a, l.a)); break;
                  case BlendMode.Overlay:   result = new Color(OverlayChannel(b.r, l.r), OverlayChannel(b.g, l.g), OverlayChannel(b.b, l.b), OverlayChannel(b.a, l.a)); break;
                  case BlendMode.SoftLight: result = new Color(SoftLightChannel(b.r, l.r), SoftLightChannel(b.g, l.g), SoftLightChannel(b.b, l.b), SoftLightChannel(b.a, l.a)); break;
                  case BlendMode.HardLight: result = new Color(OverlayChannel(l.r, b.r), OverlayChannel(l.g, b.g), OverlayChannel(l.b, b.b), OverlayChannel(l.a, b.a)); break;
              }
              return result;
          }

          private static float OverlayChannel(float b, float l)
              => b < 0.5f ? 2f * b * l : 1f - 2f * (1f - b) * (1f - l);

          private static float SoftLightChannel(float b, float l)
              => l < 0.5f
                  ? b - (1f - 2f * l) * b * (1f - b)
                  : b + (2f * l - 1f) * (D(b) - b);

          private static float D(float b) => b <= 0.25f ? ((16f * b - 12f) * b + 4f) * b : Mathf.Sqrt(b);
      }
  }
  ```

- [ ] **Step 2: Verify compilation**
  Verify there are no syntax errors in `TextureMixer.cs`.

---

### Task 3: Create TextureUnpacker.cs

**Files:**
- Create: `p:/Personal/00 Unity/03 RexTools/RexTools/RexTools-Unity6/Editor/Texture Repacker/TextureUnpacker.cs`

- [ ] **Step 1: Write unpacking engine class**
  Create the file with the following complete code content:
  ```csharp
  using UnityEngine;
  using System.IO;

  namespace RexTools.TextureRepacker.Editor
  {
      public static class TextureUnpacker
      {
          public static void Unpack(
              Texture2D source,
              bool[] activeChannels,
              string[] suffixes,
              bool[] invert,
              string outputName,
              string outputPath,
              System.Action<string, float> progressCallback = null)
          {
              if (source == null) return;

              Color[] pixels = TextureRepackerUtils.GetReadablePixels(source);
              int w = source.width;
              int h = source.height;

              for (int i = 0; i < 4; i++)
              {
                  if (!activeChannels[i]) continue;

                  if (progressCallback != null)
                  {
                      progressCallback($"Extracting channel {i}...", (float)i / 4f);
                  }

                  Texture2D res = new Texture2D(w, h, TextureFormat.RGB24, false);
                  Color[] resPixels = new Color[pixels.Length];
                  bool inv = invert[i];

                  for (int p = 0; p < pixels.Length; p++)
                  {
                      float val = i == 0 ? pixels[p].r : i == 1 ? pixels[p].g : i == 2 ? pixels[p].b : pixels[p].a;
                      if (inv) val = 1f - val;
                      resPixels[p] = new Color(val, val, val, 1f);
                  }
                  res.SetPixels(resPixels);
                  res.Apply();

                  string outFilePath = Path.Combine(outputPath, outputName + suffixes[i] + ".png").Replace('\\', '/');
                  File.WriteAllBytes(outFilePath, res.EncodeToPNG());
                  Object.DestroyImmediate(res);
              }
          }

          public static Texture2D GenerateChannelPreview(
              Texture2D source,
              int channel,
              bool invert,
              System.Action<string, float> progressCallback = null)
          {
              if (source == null) return null;

              int w = source.width;
              int h = source.height;
              Texture2D result = new Texture2D(w, h, TextureFormat.RGBA32, false);
              Color[] pixels = new Color[w * h];
              Color[] srcPixels = TextureRepackerUtils.GetReadablePixels(source);

              int progressInterval = Mathf.Max(1, h / 20);
              for (int y = 0; y < h; y++)
              {
                  if (y % progressInterval == 0 && progressCallback != null)
                  {
                      progressCallback($"Processing row {y}/{h}...", (float)y / h);
                  }

                  for (int x = 0; x < w; x++)
                  {
                      Color p = srcPixels[y * w + x];
                      float val = channel == 0 ? p.r : channel == 1 ? p.g : channel == 2 ? p.b : p.a;
                      if (invert) val = 1f - val;
                      pixels[y * w + x] = new Color(val, val, val, 1f);
                  }
              }

              result.SetPixels(pixels);
              result.Apply();
              return result;
          }
      }
  }
  ```

- [ ] **Step 2: Verify compilation**
  Verify there are no syntax errors in `TextureUnpacker.cs`.

---

### Task 4: Create TexturePacker.cs

**Files:**
- Create: `p:/Personal/00 Unity/03 RexTools/RexTools/RexTools-Unity6/Editor/Texture Repacker/TexturePacker.cs`

- [ ] **Step 1: Write packing engine class**
  Create the file with the following complete code content:
  ```csharp
  using UnityEngine;
  using System.Linq;

  namespace RexTools.TextureRepacker.Editor
  {
      public class ChannelSlotData
      {
          public Texture2D texture;
          public int channelIndex = 0; // 0=R, 1=G, 2=B, 3=A
          public bool invert = false;
          public bool useCustom = false;
          public float customValue = 0.5f;
      }

      public static class TexturePacker
      {
          public static Texture2D Pack(
              ChannelSlotData[] slots,
              int width,
              int height,
              System.Action<string, float> progressCallback = null)
          {
              Texture2D result = new Texture2D(width, height, TextureFormat.RGBA32, false);
              Color[] pixels = new Color[width * height];

              Color[][] slotPixels = new Color[4][];
              int[] slotW = new int[4];
              int[] slotH = new int[4];
              float[] customVals = new float[4];

              for (int c = 0; c < 4; c++)
              {
                  if (!slots[c].useCustom && slots[c].texture != null)
                  {
                      slotPixels[c] = TextureRepackerUtils.GetReadablePixels(slots[c].texture);
                      slotW[c] = slots[c].texture.width;
                      slotH[c] = slots[c].texture.height;
                  }
                  if (slots[c].useCustom)
                      customVals[c] = slots[c].invert ? 1f - slots[c].customValue : slots[c].customValue;
                  else if (slots[c].texture == null)
                      customVals[c] = (c == 3) ? 1f : 0f;
              }

              int progressInterval = Mathf.Max(1, height / 20);
              for (int y = 0; y < height; y++)
              {
                  if (y % progressInterval == 0 && progressCallback != null)
                  {
                      progressCallback($"Processing row {y}/{height}...", (float)y / height);
                  }

                  for (int x = 0; x < width; x++)
                  {
                      float[] channels = new float[4];
                      for (int c = 0; c < 4; c++)
                      {
                          if (slots[c].useCustom || slots[c].texture == null)
                          {
                              channels[c] = customVals[c];
                          }
                          else
                          {
                              int srcX = Mathf.Clamp(x * slotW[c] / width, 0, slotW[c] - 1);
                              int srcY = Mathf.Clamp(y * slotH[c] / height, 0, slotH[c] - 1);
                              Color p = slotPixels[c][srcY * slotW[c] + srcX];
                              float val = slots[c].channelIndex == 0 ? p.r : slots[c].channelIndex == 1 ? p.g : slots[c].channelIndex == 2 ? p.b : p.a;
                              if (slots[c].invert) val = 1f - val;
                              channels[c] = val;
                          }
                      }
                      pixels[y * width + x] = new Color(channels[0], channels[1], channels[2], channels[3]);
                  }
              }

              result.SetPixels(pixels);
              result.Apply();
              return result;
          }

          public static float[] SampleSlotChannel(ChannelSlotData slot, int size)
          {
              int totalPixels = size * size;
              float[] result = new float[totalPixels];

              if (slot.useCustom)
              {
                  float val = slot.invert ? 1f - slot.customValue : slot.customValue;
                  System.Array.Fill(result, val);
                  return result;
              }

              if (slot.texture == null)
              {
                  float val = (slot.channelIndex == 3) ? 1f : 0f;
                  System.Array.Fill(result, val);
                  return result;
              }

              Color[] srcPixels = TextureRepackerUtils.GetReadablePixels(slot.texture);
              int srcW = slot.texture.width;
              int srcH = slot.texture.height;
              int channel = slot.channelIndex;
              bool invert = slot.invert;

              for (int y = 0; y < size; y++)
              {
                  int srcY = Mathf.Clamp(y * srcH / size, 0, srcH - 1);
                  for (int x = 0; x < size; x++)
                  {
                      int srcX = Mathf.Clamp(x * srcW / size, 0, srcW - 1);
                      Color p = srcPixels[srcY * srcW + srcX];
                      float val = channel == 0 ? p.r : channel == 1 ? p.g : channel == 2 ? p.b : p.a;
                      if (invert) val = 1f - val;
                      result[y * size + x] = val;
                  }
              }
              return result;
          }
      }
  }
  ```

- [ ] **Step 2: Verify compilation**
  Verify there are no syntax errors in `TexturePacker.cs`.

---

### Task 5: Create TextureRepackerPreviewWindow.cs

**Files:**
- Create: `p:/Personal/00 Unity/03 RexTools/RexTools/RexTools-Unity6/Editor/Texture Repacker/TextureRepackerPreviewWindow.cs`

- [ ] **Step 1: Write live preview popup window class**
  Create the file with the following complete code content:
  ```csharp
  using UnityEngine;
  using UnityEditor;
  using UnityEngine.UIElements;

  namespace RexTools.TextureRepacker.Editor
  {
      public class TextureRepackerPreviewWindow : EditorWindow
      {
          private Image _image;
          private TexturePackSeparator _owner;
          private int _mode;
          private Texture2D _highResTexture;
          private Label _resLabel;

          public static void ShowWindow(TexturePackSeparator owner, int mode, string title)
          {
              var window = GetWindow<TextureRepackerPreviewWindow>("Rex Tools - " + title);
              window._owner = owner;
              window._mode = mode;
              window.minSize = new Vector2(512, 512);
              window.RefreshHighRes();
          }

          private void CreateGUI()
          {
              var toolbar = new VisualElement 
              { 
                  style = { flexDirection = FlexDirection.Row, backgroundColor = new Color(0.2f, 0.2f, 0.2f), paddingLeft = 5, paddingRight = 5, height = 25, alignItems = Align.Center } 
              };
              
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

              if (_highResTexture != null) Object.DestroyImmediate(_highResTexture);
              _highResTexture = _owner.GenerateFullResResult(_mode);

              if (_highResTexture != null)
              {
                  if (_image != null) _image.image = _highResTexture;
                  if (_resLabel != null) _resLabel.text = $"Resolution: {_highResTexture.width} x {_highResTexture.height}";
              }
          }

          private void OnDestroy()
          {
              if (_highResTexture != null) Object.DestroyImmediate(_highResTexture);
          }
      }
  }
  ```

- [ ] **Step 2: Verify compilation**
  Verify there are no syntax errors in `TextureRepackerPreviewWindow.cs`.

---

### Task 6: Refactor TexturePackSeparator.cs

**Files:**
- Modify: `p:/Personal/00 Unity/03 RexTools/RexTools/RexTools-Unity6/Editor/Texture Repacker/TexturePackSeparator.cs`

- [ ] **Step 1: Replace implementation of TexturePackSeparator**
  Replace the entire content of `TexturePackSeparator.cs` with the updated class that delegates processing to the new modular components. Ensure it matches exactly:
  ```csharp
  using UnityEngine;
  using UnityEditor;
  using UnityEngine.UIElements;
  using UnityEditor.UIElements;
  using System.IO;
  using System.Collections.Generic;
  using System.Linq;
  using RexTools.Editor.Core;

  namespace RexTools.TextureRepacker.Editor
  {
      public class TexturePackSeparator : EditorWindow
      {
          private ChannelSlotData[] packSlots = new ChannelSlotData[4] {
              new ChannelSlotData { channelIndex = 0 }, // Red
              new ChannelSlotData { channelIndex = 1 }, // Green
              new ChannelSlotData { channelIndex = 2 }, // Blue
              new ChannelSlotData { channelIndex = 3 }  // Alpha
          };

          private string outputName = "PackedTexture";
          private string outputPath = "Assets";
          private int currentTabIndex = 0;

          // Performance: preview debounce
          private bool _previewDirty = false;
          private IVisualElementScheduledItem _previewSchedule;

          // Unpack settings
          private Texture2D unpackSource;
          private bool[] unpackModes = { true, true, true, false }; // R, G, B, A
          private string[] unpackSuffixes = { "_R", "_G", "_B", "_A" };
          private string unpackOutputName = "UnpackedTexture";
          private string unpackOutputPath = "Assets";
          private bool[] unpackInvert = { false, false, false, false }; // R, G, B, A

          // Mix settings
          private Texture2D mixBase;
          private Texture2D mixLayer;
          private int mixBaseChannel  = -1; // -1 = Full RGBA, 0-3 = R/G/B/A
          private int mixLayerChannel = -1;
          private BlendMode mixBlendMode = BlendMode.Multiply;
          private float mixOpacity = 1f;
          private string mixOutputName = "MixedTexture";
          private string mixOutputPath = "Assets";

          // UI
          private RexTexturePreview combinedPreview;
          private VisualElement packContainer;
          private VisualElement unpackContainer;
          private VisualElement mixContainer;
          private RexActionButton actionButton;
          private List<RexSlider> slotSliders = new List<RexSlider>();
          private RexTabGroup tabGroup;
          private TextField nameField;
          private RexFolderSelector folderZone;
          private TextField unpackNameField;
          private RexFolderSelector unpackFolderZone;
          private TextField mixNameField;
          private RexFolderSelector mixFolderZone;
          private RexTexturePreview mixPreviewImage;
          private Texture2D mixPreviewBuffer;
          private bool _mixPreviewDirty = false;
          private IVisualElementScheduledItem _mixPreviewSchedule;
          private Texture2D previewBuffer;
          private int debugPreviewMode = 0; // 0=RGBA, 1=R, 2=G, 3=B, 4=A
          private bool showHelp = false;
          private RexHelpBox helpBox;
          private List<RexButton> debugButtons = new List<RexButton>();
          private List<List<RexButton>> slotChannelButtons = new List<List<RexButton>>();
          private List<RexButton> slotTexModeButtons = new List<RexButton>();
          private List<RexButton> slotValModeButtons = new List<RexButton>();
          private List<VisualElement> slotTextureContainers = new List<VisualElement>();
          private List<VisualElement> slotValueContainers = new List<VisualElement>();
          private List<RexTextureField> slotDropZones = new List<RexTextureField>();
          private RexTexturePreview[] unpackPreviews = new RexTexturePreview[4];
          private Texture2D[] unpackPreviewBuffers = new Texture2D[4];

          [MenuItem("Tools/Rex Tools/Texture Repacker")]
          public static void ShowWindow() {
              var window = GetWindow<TexturePackSeparator>("Texture Repacker");
              window.minSize = new Vector2(450, 750);
          }

          public void CreateGUI()
          {
              VisualElement root = rootVisualElement;
              root.style.paddingTop = root.style.paddingBottom = 12;
              root.style.paddingLeft = root.style.paddingRight = 0;

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

              // --- BRANDED HEADER & HELP BOX ---
              helpBox = new RexHelpBox(
                  "PACK: Drop textures into RGBA slots. Swizzle channels with R|G|B|A buttons.",
                  "VAL: Toggle to use a raw value instead of a texture channel.",
                  "UNPACK: Drop a texture to extract its channels into separate files.",
                  "MIX: Drop Base + Layer textures. Select blend mode and optional channel. Click MIX to save result."
              );
              helpBox.style.marginLeft = 12;
              helpBox.style.marginRight = 12;

              var header = new RexHeader("Texture Repacker", showHelpButton: true);
              header.style.paddingLeft = 12;
              header.style.paddingRight = 12;
              header.OnHelpClicked += () => {
                  showHelp = !showHelp;
                  helpBox.ToggleVisibility();
                  header.SetHelpButtonActive(showHelp);
              };

              root.Add(header);
              root.Add(helpBox);

              // Tabs Header
              tabGroup = new RexTabGroup(new string[] { "PACK", "UNPACK", "MIX" });
              tabGroup.style.marginLeft = 12;
              tabGroup.style.marginRight = 12;
              tabGroup.style.marginBottom = 15;
              tabGroup.style.height = 30;
              tabGroup.style.flexShrink = 0;
              tabGroup.OnTabChanged += SwitchTab;
              root.Add(tabGroup);

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
              actionButton = new RexActionButton("PACK") { style = { flexShrink = 0, marginLeft = 12, marginRight = 12 } };
              actionButton.OnClick += Process;
              root.Add(actionButton);

              SwitchTab(currentTabIndex);
          }

          private void SwitchTab(int index)
          {
              currentTabIndex = index;
              packContainer.style.display   = index == 0 ? DisplayStyle.Flex : DisplayStyle.None;
              unpackContainer.style.display = index == 1 ? DisplayStyle.Flex : DisplayStyle.None;
              mixContainer.style.display    = index == 2 ? DisplayStyle.Flex : DisplayStyle.None;

              string[] labels = { "PACK", "UNPACK", "MIX" };
              actionButton.Label = labels[index];
              
              if (index == 0)
                  actionButton.Tint = new Color(0.2f, 0.6f, 0.3f);
              else if (index == 1)
                  actionButton.Tint = new Color(0.7f, 0.3f, 0.2f);
              else
                  actionButton.Tint = new Color(0.2f, 0.5f, 0.8f);

              tabGroup?.SetSelectedTabWithoutNotify(index);

              if (index == 0) UpdatePreview();
              if (index == 1) UpdateUnpackPreviews();
              if (index == 2) UpdateMixPreview();
          }

          private void SetupPackUI()
          {
              slotChannelButtons.Clear();
              slotTexModeButtons.Clear();
              slotValModeButtons.Clear();
              slotTextureContainers.Clear();
              slotValueContainers.Clear();
              slotDropZones.Clear();
              slotSliders.Clear();

              var previewSection = new VisualElement { style = { marginBottom = 15, flexDirection = FlexDirection.Row, justifyContent = Justify.Center, alignItems = Align.Center, flexShrink = 0, height = 180 } };
              
              combinedPreview = new RexTexturePreview(160, "Show full-size preview", "packed texture preview");
              combinedPreview.style.marginRight = 10;
              combinedPreview.OnMaximizeClicked += () => TextureRepackerPreviewWindow.ShowWindow(this, 0, "Pack Preview");
              previewSection.Add(combinedPreview);

              var debugColumn = new VisualElement { style = { flexDirection = FlexDirection.Column, justifyContent = Justify.Center } };
              debugButtons.Clear();
              string[] modes = { "RGBA", "R", "G", "B", "A" };
              for (int i = 0; i < modes.Length; i++) {
                  int m = i;
                  var btn = new RexButton(modes[i]);
                  btn.style.width = 45;
                  btn.style.height = 25;
                  btn.style.fontSize = 9;
                  btn.style.marginBottom = 4;
                  btn.OnClick += () => { debugPreviewMode = m; UpdatePreview(); };
                  debugButtons.Add(btn);
                  debugColumn.Add(btn);
              }
              previewSection.Add(debugColumn);
              packContainer.Add(previewSection);

              var saveBox = new VisualElement { style = { flexShrink = 0 } };
              saveBox.AddToClassList("rex-box");
              saveBox.Add(new Label("OUTPUT SETTINGS") { style = { unityFontStyleAndWeight = FontStyle.Bold, fontSize = 10, marginBottom = 5, color = Color.gray } });
              
              var nameRow = new VisualElement();
              nameRow.AddToClassList("rex-row");
              
              var nameLabel = new Label("Name:") { style = { width = 50, flexShrink = 0 } };
              nameRow.Add(nameLabel);
              
              nameField = new TextField { value = outputName };
              nameField.RegisterValueChangedCallback(e => outputName = e.newValue);
              nameRow.Add(nameField);
              saveBox.Add(nameRow);
   
              var pathRow = new VisualElement();
              pathRow.AddToClassList("rex-row");
              pathRow.style.alignItems = Align.FlexStart;
              
              var pathLabel = new Label("Path:") { style = { width = 50, flexShrink = 0, marginTop = 3 } };
              pathRow.Add(pathLabel);
              
              folderZone = new RexFolderSelector(required: true);
              folderZone.SetPathWithoutNotify(outputPath);
              folderZone.style.flexGrow = 1;
              folderZone.OnValueChanged += p => outputPath = p;
              pathRow.Add(folderZone);
              saveBox.Add(pathRow);
              
              packContainer.Add(saveBox);

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
                  slot.style.width = 200;
                  slot.Add(new Label(names[i]) { style = { unityFontStyleAndWeight = FontStyle.Bold, fontSize = 10, marginBottom = 4, color = colors[i] } });
                  
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

                  var valueContainer = new VisualElement();
                  valueContainer.style.marginTop = 4;
                  
                  var slider = new RexSlider(0f, 1f, defaultValue: 0.5f, value: packSlots[index].customValue);
                  slider.AddToClassList("rex-field-flex");
                  slider.style.height = 28;
                  slider.OnValueChanged += val => {
                      packSlots[index].customValue = val;
                      UpdatePreview();
                  };
                  valueContainer.Add(slider);
                  slotSliders.Add(slider);
                  
                  slot.Add(valueContainer);
                  slotValueContainers.Add(valueContainer);
                  
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

                  textureContainer.style.display = packSlots[index].useCustom ? DisplayStyle.None : DisplayStyle.Flex;
                  valueContainer.style.display = packSlots[index].useCustom ? DisplayStyle.Flex : DisplayStyle.None;

                  grid.Add(slot);
              }
              channelsBox.Add(grid);
              packContainer.Add(channelsBox);
          }

          private void OnSlotTextureDropped(Texture2D tex)
          {
              if (tex == null) return;
              TextureRepackerUtils.RemoveFromCache(tex.GetInstanceID());
              if (outputName == "PackedTexture" || string.IsNullOrEmpty(outputName)) {
                  outputName = TextureRepackerUtils.GenerateBaseName(tex.name);
                  nameField.value = outputName;
              }
              if (string.IsNullOrEmpty(outputPath) || outputPath == "Assets") {
                  outputPath = Path.GetDirectoryName(AssetDatabase.GetAssetPath(tex));
                  folderZone.SetPathWithoutNotify(outputPath);
              }
          }

          private void SetupUnpackUI()
          {
              var sourceBox = new VisualElement { style = { flexShrink = 0, marginBottom = 15 } };
              sourceBox.AddToClassList("rex-box");
              sourceBox.Add(new Label("SOURCE TEXTURE") { style = { unityFontStyleAndWeight = FontStyle.Bold, fontSize = 10, marginBottom = 5, color = Color.gray } });
              
              var drop = new RexTextureField("Drop Texture to Unpack", 100);
              drop.OnTextureChanged = tex => {
                  unpackSource = tex;
                  if (tex != null) {
                      if (string.IsNullOrEmpty(unpackOutputPath) || unpackOutputPath == "Assets") {
                          unpackOutputPath = Path.GetDirectoryName(AssetDatabase.GetAssetPath(tex));
                          unpackFolderZone.SetPathWithoutNotify(unpackOutputPath);
                      }
                      if (unpackOutputName == "UnpackedTexture") {
                          unpackOutputName = TextureRepackerUtils.GenerateBaseName(tex.name);
                          unpackNameField.value = unpackOutputName;
                      }
                  }
                  UpdateUnpackPreviews();
              };
              sourceBox.Add(drop);
              unpackContainer.Add(sourceBox);

              var outputBox = new VisualElement { style = { flexShrink = 0, marginBottom = 15 } };
              outputBox.AddToClassList("rex-box");
              outputBox.Add(new Label("OUTPUT SETTINGS") { style = { unityFontStyleAndWeight = FontStyle.Bold, fontSize = 10, marginBottom = 5, color = Color.gray } });

              var nameRow = new VisualElement();
              nameRow.AddToClassList("rex-row");
              
              var nameLabel = new Label("Name:") { style = { width = 50, flexShrink = 0 } };
              nameRow.Add(nameLabel);
              
              unpackNameField = new TextField { value = unpackOutputName };
              unpackNameField.RegisterValueChangedCallback(e => {
                  unpackOutputName = e.newValue;
              });
              nameRow.Add(unpackNameField);
              outputBox.Add(nameRow);

              var unpackPathRow = new VisualElement();
              unpackPathRow.AddToClassList("rex-row");
              unpackPathRow.style.alignItems = Align.FlexStart;
              
              var pathLabel = new Label("Path:") { style = { width = 50, flexShrink = 0, marginTop = 3 } };
              unpackPathRow.Add(pathLabel);
              
              unpackFolderZone = new RexFolderSelector(required: true);
              unpackFolderZone.SetPathWithoutNotify(unpackOutputPath);
              unpackFolderZone.style.flexGrow = 1;
              unpackFolderZone.OnValueChanged += p => {
                  unpackOutputPath = p;
              };
              unpackPathRow.Add(unpackFolderZone);
              outputBox.Add(unpackPathRow);
              unpackContainer.Add(outputBox);

              var channelsBox = new VisualElement { style = { flexShrink = 0 } };
              channelsBox.AddToClassList("rex-box");
              channelsBox.Add(new Label("CHANNEL EXTRACTION") { style = { unityFontStyleAndWeight = FontStyle.Bold, fontSize = 10, marginBottom = 5, color = Color.gray } });
              
              var grid = new VisualElement { style = { flexDirection = FlexDirection.Row, flexWrap = Wrap.Wrap, justifyContent = Justify.SpaceBetween, flexShrink = 0 } };
              grid.AddToClassList("rex-grid");
              
              string[] names = { "RED (R)", "GREEN (G)", "BLUE (B)", "ALPHA (A)" };
              Color[] colors = { new Color(1, 0.3f, 0.3f), new Color(0.3f, 1, 0.3f), new Color(0.3f, 0.6f, 1), Color.white };

              for (int i = 0; i < 4; i++) {
                  int idx = i;
                  var slot = new VisualElement();
                  slot.AddToClassList("rex-box");
                  slot.style.width = 200;

                  var headerRow = new VisualElement();
                  headerRow.AddToClassList("rex-row");
                  headerRow.style.justifyContent = Justify.SpaceBetween;
                  headerRow.style.marginBottom = 6;
                  headerRow.style.alignItems = Align.Center;

                  var chanLabel = new Label(names[idx]) { style = { unityFontStyleAndWeight = FontStyle.Bold, fontSize = 10, color = colors[idx] } };
                  var toggle = new Toggle { value = unpackModes[idx], style = { marginRight = 0 } };

                  headerRow.Add(chanLabel);
                  headerRow.Add(toggle);
                  slot.Add(headerRow);

                  var preview = new RexTexturePreview(90, $"Show full-size {names[idx]} preview", "channel preview");
                  preview.style.alignSelf = Align.Center;
                  preview.style.marginTop = 4;
                  preview.style.marginBottom = 6;
                  preview.OnMaximizeClicked += () => TextureRepackerPreviewWindow.ShowWindow(this, 10 + idx, $"{names[idx]} Channel Preview");
                  unpackPreviews[idx] = preview;
                  slot.Add(preview);

                  var suffixRow = new VisualElement();
                  suffixRow.AddToClassList("rex-row");
                  var suffixLabel = new Label("Suffix:") { style = { fontSize = 10, color = Color.gray, width = 45, flexShrink = 0 } };
                  var suffix = new TextField { value = unpackSuffixes[idx] };
                  suffix.AddToClassList("rex-field-flex");
                  suffixRow.Add(suffixLabel);
                  suffixRow.Add(suffix);
                  slot.Add(suffixRow);

                  var invertRow = new VisualElement();
                  invertRow.AddToClassList("rex-row");
                  invertRow.style.justifyContent = Justify.Center;
                  var invertBtn = new RexButton("Invert Channel", isToggle: true, defaultActive: unpackInvert[idx]);
                  invertBtn.style.flexGrow = 1;
                  invertBtn.style.height = 18;
                  invertBtn.style.fontSize = 9;
                  invertBtn.OnToggleChanged += active => {
                      unpackInvert[idx] = active;
                      UpdateUnpackPreviews();
                  };
                  invertRow.Add(invertBtn);
                  slot.Add(invertRow);

                  toggle.RegisterValueChangedCallback(e => {
                      unpackModes[idx] = e.newValue;
                      suffix.SetEnabled(e.newValue);
                      invertBtn.SetEnabled(e.newValue);
                      preview.SetEnabled(e.newValue);
                      slot.style.opacity = e.newValue ? 1f : 0.4f;
                  });
                  suffix.RegisterValueChangedCallback(e => {
                      unpackSuffixes[idx] = e.newValue;
                  });

                  suffix.SetEnabled(unpackModes[idx]);
                  invertBtn.SetEnabled(unpackModes[idx]);
                  preview.SetEnabled(unpackModes[idx]);
                  slot.style.opacity = unpackModes[idx] ? 1f : 0.4f;

                  grid.Add(slot);
              }
              channelsBox.Add(grid);
              unpackContainer.Add(channelsBox);
          }

          private void UpdatePreview()
          {
              if (combinedPreview == null) return;
              UpdateButtonStates();

              _previewDirty = true;
              if (_previewSchedule == null) {
                  _previewSchedule = rootVisualElement.schedule.Execute(RebuildPreview).Every(100);
              }
          }

          private void UpdateButtonStates()
          {
              for (int i = 0; i < debugButtons.Count; i++) {
                  debugButtons[i].IsActive = (debugPreviewMode == i);
              }
              for (int i = 0; i < slotChannelButtons.Count; i++) {
                  bool custom = packSlots[i].useCustom;
                  slotTexModeButtons[i].IsActive = !custom;
                  slotValModeButtons[i].IsActive = custom;
                  slotTextureContainers[i].style.display = custom ? DisplayStyle.None : DisplayStyle.Flex;
                  slotValueContainers[i].style.display = custom ? DisplayStyle.Flex : DisplayStyle.None;
                  
                  for (int c = 0; c < slotChannelButtons[i].Count; c++) {
                      slotChannelButtons[i][c].IsActive = (packSlots[i].channelIndex == c);
                  }
                  if (custom) {
                      slotDropZones[i].SetColor(new Color(packSlots[i].customValue, packSlots[i].customValue, packSlots[i].customValue, 1f));
                  } else {
                      slotDropZones[i].ClearColor();
                  }
                  if (i < slotSliders.Count) {
                      slotSliders[i].SetEnabled(custom);
                  }
              }
          }

          private void RebuildPreview()
          {
              if (!_previewDirty) return;
              _previewDirty = false;

              bool hasAnyInput = packSlots.Any(s => s.useCustom || s.texture != null);
              if (!hasAnyInput) {
                  combinedPreview.image = null;
                  return;
              }

              const int size = 128;
              int totalPixels = size * size;
              if (previewBuffer == null) previewBuffer = new Texture2D(size, size, TextureFormat.RGBA32, false);

              float[][] slotValues = new float[4][];
              for (int i = 0; i < 4; i++) {
                  slotValues[i] = TexturePacker.SampleSlotChannel(packSlots[i], size);
              }

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

          private void UpdateUnpackPreviews()
          {
              if (unpackSource == null) {
                  for (int i = 0; i < 4; i++) {
                      if (unpackPreviews[i] != null) unpackPreviews[i].image = null;
                  }
                  return;
              }

              const int size = 32;
              int total = size * size;
              Color[] srcPixels = TextureRepackerUtils.GetReadablePixels(unpackSource);
              int srcW = unpackSource.width;
              int srcH = unpackSource.height;

              for (int i = 0; i < 4; i++) {
                  if (unpackPreviews[i] == null) continue;

                  if (unpackPreviewBuffers[i] == null) {
                      unpackPreviewBuffers[i] = new Texture2D(size, size, TextureFormat.RGB24, false);
                  }

                  Color[] pixels = new Color[total];
                  bool invert = unpackInvert[i];
                  int channel = i;

                  for (int y = 0; y < size; y++) {
                      int srcY = Mathf.Clamp(y * srcH / size, 0, srcH - 1);
                      for (int x = 0; x < size; x++) {
                          int srcX = Mathf.Clamp(x * srcW / size, 0, srcW - 1);
                          Color p = srcPixels[srcY * srcW + srcX];
                          float val = channel == 0 ? p.r : channel == 1 ? p.g : channel == 2 ? p.b : p.a;
                          if (invert) val = 1f - val;
                          pixels[y * size + x] = new Color(val, val, val, 1f);
                      }
                  }

                  unpackPreviewBuffers[i].SetPixels(pixels);
                  unpackPreviewBuffers[i].Apply();
                  unpackPreviews[i].image = unpackPreviewBuffers[i];
              }
          }

          private void Process() {
              if (currentTabIndex == 0) Pack();
              else if (currentTabIndex == 1) Unpack();
              else Mix();
          }

          private void Pack()
          {
              if (string.IsNullOrEmpty(outputPath)) {
                  EditorUtility.DisplayDialog("Path Required", "Please select a valid output path.", "OK");
                  return;
              }
              string finalPath = Path.Combine(outputPath, outputName + ".png").Replace('\\', '/');
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

          public Texture2D GenerateFullResResult(int tabIndex)
          {
              int w = 512, h = 512;
              string title = "Processing Texture";

              if (tabIndex == 0) {
                  title = "Packing Texture";
                  var nonNull = packSlots.Where(s => !s.useCustom && s.texture != null).ToList();
                  if (nonNull.Count > 0) {
                      w = nonNull.Max(s => s.texture.width);
                      h = nonNull.Max(s => s.texture.height);
                  }
                  return TexturePacker.Pack(packSlots, w, h, (msg, progress) => {
                      EditorUtility.DisplayProgressBar(title, msg, progress);
                  });
              } else if (tabIndex == 2) {
                  title = "Mixing Texture";
                  if (mixBase == null) return null;
                  w = mixBase.width;
                  h = mixBase.height;
                  return TextureMixer.Mix(mixBase, mixLayer, mixBaseChannel, mixLayerChannel, mixBlendMode, mixOpacity, (msg, progress) => {
                      EditorUtility.DisplayProgressBar(title, msg, progress);
                  });
              } else if (tabIndex >= 10 && tabIndex <= 13) {
                  title = "Extracting Channel Preview";
                  if (unpackSource == null) return null;
                  int channel = tabIndex - 10;
                  return TextureUnpacker.GenerateChannelPreview(unpackSource, channel, unpackInvert[channel], (msg, progress) => {
                      EditorUtility.DisplayProgressBar(title, msg, progress);
                  });
              }

              return null;
          }

          private void Unpack()
          {
              if (unpackSource == null) return;
              
              if (string.IsNullOrEmpty(unpackOutputPath)) {
                  EditorUtility.DisplayDialog("Path Required", "Please select a valid output path.", "OK");
                  return;
              }
              string finalPath = unpackOutputPath;
              string name = unpackOutputName;
              
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

              try {
                  TextureUnpacker.Unpack(
                      unpackSource,
                      unpackModes,
                      unpackSuffixes,
                      unpackInvert,
                      name,
                      finalPath,
                      (msg, progress) => {
                          EditorUtility.DisplayProgressBar("Unpacking Texture", msg, progress);
                      }
                  );
                  AssetDatabase.Refresh();
                  EditorUtility.DisplayDialog("Success", "Unpacking complete!", "OK");
              } finally {
                  EditorUtility.ClearProgressBar();
              }
          }

          private void SetupMixUI()
          {
              var previewSection = new VisualElement { style = { marginBottom = 15, flexDirection = FlexDirection.Row, justifyContent = Justify.Center, alignItems = Align.Center, flexShrink = 0, height = 180 } };
              
              mixPreviewImage = new RexTexturePreview(160, "Show full-size preview", "mixed texture preview");
              mixPreviewImage.OnMaximizeClicked += () => TextureRepackerPreviewWindow.ShowWindow(this, 2, "Mix Preview");
              previewSection.Add(mixPreviewImage);
              mixContainer.Add(previewSection);

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
              
              var opacityLabel = new Label("Opacity:") { style = { width = 55, flexShrink = 0 } };
              opacityRow.Add(opacityLabel);
              
              var opacitySlider = new RexSlider(0f, 1f, defaultValue: 1f, value: mixOpacity);
              opacitySlider.AddToClassList("rex-field-flex");
              opacitySlider.OnValueChanged += val => {
                  mixOpacity = val;
                  UpdateMixPreview();
              };
              opacityRow.Add(opacitySlider);
              blendBox.Add(opacityRow);
              mixContainer.Add(blendBox);

              string[] texLabels = { "BASE", "LAYER" };
              for (int t = 0; t < 2; t++) {
                  int ti = t;
                  var box = new VisualElement { style = { flexShrink = 0, marginBottom = 10 } };
                  box.AddToClassList("rex-box");
                  box.Add(new Label(texLabels[t]) { style = { unityFontStyleAndWeight = FontStyle.Bold, fontSize = 10, marginBottom = 5, color = ti == 0 ? new Color(0.5f, 0.8f, 1f) : new Color(1f, 0.7f, 0.4f) } });

                  var drop = new RexTextureField("Drop " + texLabels[t] + " Texture", 90);
                  drop.OnTextureChanged = tex => {
                      if (ti == 0) {
                          mixBase = tex;
                          if (tex != null) { TextureRepackerUtils.RemoveFromCache(tex.GetInstanceID()); }
                          if (tex != null && (mixOutputName == "MixedTexture" || string.IsNullOrEmpty(mixOutputName))) {
                              mixOutputName = TextureRepackerUtils.GenerateBaseName(tex.name) + "_mixed";
                              if (mixNameField != null) mixNameField.value = mixOutputName;
                          }
                          if (tex != null && (string.IsNullOrEmpty(mixOutputPath) || mixOutputPath == "Assets")) {
                              mixOutputPath = Path.GetDirectoryName(AssetDatabase.GetAssetPath(tex));
                              if (mixFolderZone != null) mixFolderZone.SetPathWithoutNotify(mixOutputPath);
                          }
                      } else {
                          mixLayer = tex;
                          if (tex != null) { TextureRepackerUtils.RemoveFromCache(tex.GetInstanceID()); }
                      }
                      UpdateMixPreview();
                  };
                  box.Add(drop);

                  var chanRow = new VisualElement();
                  chanRow.AddToClassList("rex-row");
                  chanRow.style.marginTop = 5;
                  
                  var chanLabel = new Label("Channel:") { style = { width = 55, fontSize = 10, color = Color.gray, flexShrink = 0 } };
                  chanRow.Add(chanLabel);
                  string[] chanLabels = { "Full", "R", "G", "B", "A" };
                  var chanBtns = new List<RexButton>();
                  for (int c = 0; c < 5; c++) {
                      int ci = c;
                      var btn = new RexButton(chanLabels[c]);
                      btn.style.width = 34;
                      btn.style.height = 20;
                      btn.style.fontSize = 9;
                      btn.style.marginRight = 2;
                      int channelValue = ci - 1;
                      btn.OnClick += () => {
                          if (ti == 0) mixBaseChannel  = channelValue;
                          else         mixLayerChannel = channelValue;
                          
                          for (int j = 0; j < chanBtns.Count; j++) {
                              chanBtns[j].IsActive = (j == ci);
                          }
                          UpdateMixPreview();
                      };
                      chanBtns.Add(btn);
                      chanRow.Add(btn);
                  }
                  chanBtns[0].IsActive = true;
                  box.Add(chanRow);
                  mixContainer.Add(box);
              }

              var outBox = new VisualElement { style = { flexShrink = 0 } };
              outBox.AddToClassList("rex-box");
              outBox.Add(new Label("OUTPUT SETTINGS") { style = { unityFontStyleAndWeight = FontStyle.Bold, fontSize = 10, marginBottom = 5, color = Color.gray } });

              var nameRow2 = new VisualElement();
              nameRow2.AddToClassList("rex-row");
              
              var nameLabel2 = new Label("Name:") { style = { width = 50, flexShrink = 0 } };
              nameRow2.Add(nameLabel2);
              
              mixNameField = new TextField { value = mixOutputName };
              mixNameField.RegisterValueChangedCallback(e => mixOutputName = e.newValue);
              nameRow2.Add(mixNameField);
              outBox.Add(nameRow2);

              var mixPathRow = new VisualElement();
              mixPathRow.AddToClassList("rex-row");
              mixPathRow.style.alignItems = Align.FlexStart;
              
              var pathLabel2 = new Label("Path:") { style = { width = 50, flexShrink = 0, marginTop = 3 } };
              mixPathRow.Add(pathLabel2);
              
              mixFolderZone = new RexFolderSelector(required: true);
              mixFolderZone.SetPathWithoutNotify(mixOutputPath);
              mixFolderZone.style.flexGrow = 1;
              mixFolderZone.OnValueChanged += p => mixOutputPath = p;
              mixPathRow.Add(mixFolderZone);
              outBox.Add(mixPathRow);
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

              if (mixBase == null) {
                  mixPreviewImage.image = null;
                  return;
              }

              const int size = 128;
              int total = size * size;
              if (mixPreviewBuffer == null) mixPreviewBuffer = new Texture2D(size, size, TextureFormat.RGBA32, false);

              Color[] basePixels  = mixBase  != null ? TextureRepackerUtils.GetReadablePixels(mixBase)  : null;
              Color[] layerPixels = mixLayer != null ? TextureRepackerUtils.GetReadablePixels(mixLayer) : null;
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

                      Color finalBase  = TextureMixer.ApplyChannelSelect(bc, mixBaseChannel);
                      Color finalLayer = TextureMixer.ApplyChannelSelect(lc, mixLayerChannel);

                      Color blended = TextureMixer.BlendColors(finalBase, finalLayer, mixBlendMode);
                      pixels[y * size + x] = Color.Lerp(finalBase, blended, mixOpacity);
                  }
              }

              mixPreviewBuffer.SetPixels(pixels);
              mixPreviewBuffer.Apply();
              mixPreviewImage.image = mixPreviewBuffer;
          }

          private void Mix()
          {
              if (mixBase == null) {
                  EditorUtility.DisplayDialog("Mix", "Please drop a Base texture first.", "OK");
                  return;
              }

              if (string.IsNullOrEmpty(mixOutputPath)) {
                  EditorUtility.DisplayDialog("Path Required", "Please select a valid output path.", "OK");
                  return;
              }

              string finalPath = Path.Combine(
                  mixOutputPath,
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

          private void OnDestroy()
          {
              TextureRepackerUtils.ClearCache();
              if (previewBuffer != null) DestroyImmediate(previewBuffer);
              if (mixPreviewBuffer != null) DestroyImmediate(mixPreviewBuffer);
              for (int i = 0; i < 4; i++) {
                  if (unpackPreviewBuffers[i] != null) DestroyImmediate(unpackPreviewBuffers[i]);
              }
          }
      }
  }
  ```

- [ ] **Step 2: Verify compilation**
  Verify the entire project compiles successfully and that the Texture Repacker Window functions perfectly.

---
