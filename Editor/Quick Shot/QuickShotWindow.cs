using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEditor.UIElements;
using System.IO;
using System;
#if REX_URP
using UnityEngine.Rendering.Universal;
#endif
using RexTools.Editor.Core;

namespace RexTools.QuickShot.Editor
{
    public class QuickShotWindow : EditorWindow
    {
        private string exportPath;
        private bool isSceneMode = false;
        private float renderScale = 1.0f;
        private bool transparentBG = false;
        private bool autoReveal = false;
        private bool autoCopy = false;

        private RexFolderSelector folderSelector;
        private VisualElement renderScaleContainer;
        private VisualElement transparentToggleContainer;
        private RexActionButton captureButton;

        [MenuItem("Tools/Rex Tools/Quick Shot")]
        public static void ShowWindow()
        {
            var window = GetWindow<QuickShotWindow>("Quick Shot");
            window.minSize = new Vector2(350, 450);
        }

        private void OnEnable()
        {
            if (string.IsNullOrEmpty(exportPath))
            {
                exportPath = Path.Combine(Directory.GetCurrentDirectory(), "QuickShots").Replace("\\", "/");
            }
        }

        public void CreateGUI()
        {
            VisualElement root = rootVisualElement;
            root.AddToClassList("rex-root-padding");

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
            var helpBox = new RexHelpBox(
                "Export Path: The folder where screenshots are saved.",
                "Capture Mode: 'Scene' captures the editor scene view, while 'Game' captures the Main Camera.",
                "Render Scale: Resolution multiplier (only active in Game capture mode).",
                "Transparent BG: Clears the background to transparent (only active in Game capture mode).",
                "Auto Open Folder: Reveals the screenshot in Explorer/Finder after capture.",
                "Auto Copy: Copies the screenshot to the system clipboard after capture."
            );

            var header = new RexHeader("Quick Shot", showHelpButton: true);
            bool showHelp = false;
            header.OnHelpClicked += () => {
                showHelp = !showHelp;
                helpBox.ToggleVisibility();
                header.SetHelpButtonActive(showHelp);
            };

            root.Add(header);
            root.Add(helpBox);

            // --- SCROLLABLE SETTINGS AREA ---
            var scrollView = new ScrollView(ScrollViewMode.Vertical);
            scrollView.style.flexGrow = 1;
            scrollView.style.marginTop = 4;
            scrollView.style.marginBottom = 4;
            root.Add(scrollView);

            // --- EXPORT PATH ---
            var exportBox = new VisualElement();
            exportBox.AddToClassList("rex-box");

            var exportLabel = new Label("EXPORT PATH");
            exportLabel.AddToClassList("rex-section-label");
            exportBox.Add(exportLabel);

            folderSelector = new RexFolderSelector();
            folderSelector.SetPathWithoutNotify(exportPath);
            folderSelector.OnValueChanged += path => exportPath = path;
            exportBox.Add(folderSelector);

            scrollView.Add(exportBox);

            // --- SETTINGS ---
            var settingsBox = new VisualElement();
            settingsBox.AddToClassList("rex-box");

            var settingsLabel = new Label("SETTINGS");
            settingsLabel.AddToClassList("rex-section-label");
            settingsBox.Add(settingsLabel);

            // Mode Toggle
            var modeRow = new VisualElement();
            modeRow.AddToClassList("rex-row-cols-2");
            var modeLabel = new Label("Capture") { style = { width = 100 } };
            modeLabel.AddToClassList("rex-col-label");
            modeRow.Add(modeLabel);
            var modeToggle = new EnumField(isSceneMode ? ShotMode.Scene : ShotMode.Game);
            modeToggle.AddToClassList("rex-col-right");
            modeToggle.RegisterValueChangedCallback(e => {
                isSceneMode = (ShotMode)e.newValue == ShotMode.Scene;
                renderScaleContainer.style.display = isSceneMode ? DisplayStyle.None : DisplayStyle.Flex;
                transparentToggleContainer.style.display = isSceneMode ? DisplayStyle.None : DisplayStyle.Flex;
            });
            modeRow.Add(modeToggle);
            settingsBox.Add(modeRow);

            // Render Scale Input
            renderScaleContainer = new VisualElement();
            renderScaleContainer.style.display = isSceneMode ? DisplayStyle.None : DisplayStyle.Flex;

            var scaleRow = new VisualElement();
            scaleRow.AddToClassList("rex-row-cols-2");
            var scaleLabel = new Label("Render Scale") { style = { width = 100 } };
            scaleLabel.AddToClassList("rex-col-label");
            scaleRow.Add(scaleLabel);
            var scaleSlider = new RexSlider(1f, 8f, defaultValue: 1f, value: renderScale, snapIncrement: 0.25f);
            scaleSlider.OnValueChanged += val => renderScale = val;
            scaleSlider.AddToClassList("rex-col-right");
            scaleRow.Add(scaleSlider);
            renderScaleContainer.Add(scaleRow);
            
            settingsBox.Add(renderScaleContainer);

            // Transparent BG
            transparentToggleContainer = new VisualElement();
            transparentToggleContainer.AddToClassList("rex-row-cols-2");
            transparentToggleContainer.style.display = isSceneMode ? DisplayStyle.None : DisplayStyle.Flex;
            var bgLabel = new Label("Transparent BG") { style = { width = 100 } };
            bgLabel.AddToClassList("rex-col-label");
            transparentToggleContainer.Add(bgLabel);
            var transparentToggle = new Toggle { value = transparentBG };
            transparentToggle.RegisterValueChangedCallback(e => transparentBG = e.newValue);
            transparentToggle.AddToClassList("rex-col-right");
            transparentToggleContainer.Add(transparentToggle);
            settingsBox.Add(transparentToggleContainer);

            scrollView.Add(settingsBox);

            // --- POST OPERATIONS ---
            var postOpsBox = new VisualElement();
            postOpsBox.AddToClassList("rex-box");

            var postOpsLabel = new Label("POST OPERATIONS");
            postOpsLabel.AddToClassList("rex-section-label");
            postOpsBox.Add(postOpsLabel);

            var postOpsRow = new VisualElement();
            postOpsRow.AddToClassList("rex-row");

            var autoOpenBtn = new RexButton($"Auto Open Folder: {(autoReveal ? "ON" : "OFF")}", isToggle: true, defaultActive: autoReveal);
            autoOpenBtn.tooltip = "Reveal screenshot in Explorer/Finder after capture";
            autoOpenBtn.OnToggleChanged += active =>
            {
                autoReveal = active;
                autoOpenBtn.Label = $"Auto Open Folder: {(active ? "ON" : "OFF")}";
            };
            postOpsRow.Add(autoOpenBtn);

            var autoCopyBtn = new RexButton($"Auto Copy: {(autoCopy ? "ON" : "OFF")}", isToggle: true, defaultActive: autoCopy);
            autoCopyBtn.style.marginLeft = 4;
            autoCopyBtn.tooltip = "Copy screenshot to system clipboard after capture";
            autoCopyBtn.OnToggleChanged += active =>
            {
                autoCopy = active;
                autoCopyBtn.Label = $"Auto Copy: {(active ? "ON" : "OFF")}";
            };
            postOpsRow.Add(autoCopyBtn);

            postOpsBox.Add(postOpsRow);

            scrollView.Add(postOpsBox);

            // --- CAPTURE BUTTON ---
            captureButton = new RexActionButton("CAPTURE SCREENSHOT");
            captureButton.OnClick += CaptureScreenshot;
            root.Add(captureButton);
        }

        private void CaptureScreenshot()
        {
            if (!Directory.Exists(exportPath))
            {
                Directory.CreateDirectory(exportPath);
            }

            Camera targetCamera = null;

            if (isSceneMode)
            {
                targetCamera = SceneView.lastActiveSceneView?.camera;
            }
            else
            {
                targetCamera = Camera.main;
            }

            if (targetCamera == null)
            {
                Debug.LogError("[RexTools] No camera found for capture.");
                return;
            }

            int width = targetCamera.pixelWidth;
            int height = targetCamera.pixelHeight;

            if (!isSceneMode)
            {
                width = (int)(width * renderScale);
                height = (int)(height * renderScale);
            }

            // Handle URP PP if present
            bool oldPost = false;
#if REX_URP
            var camData = targetCamera.GetUniversalAdditionalCameraData();
            if (camData != null)
            {
                oldPost = camData.renderPostProcessing;
                camData.renderPostProcessing = false;
            }
#endif

            // Save state
            var oldRT = targetCamera.targetTexture;
            var oldClearFlags = targetCamera.clearFlags;
            var oldBGColor = targetCamera.backgroundColor;
            bool oldForceIntoRT = targetCamera.forceIntoRenderTexture;
            
            targetCamera.forceIntoRenderTexture = true;

            var rt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
            rt.Create();
            targetCamera.targetTexture = rt;

            if (transparentBG)
            {
                targetCamera.clearFlags = CameraClearFlags.SolidColor;
                targetCamera.backgroundColor = Color.clear;
                RenderTexture.active = rt;
                GL.Clear(true, true, Color.clear);
            }

            targetCamera.Render();

            RenderTexture.active = rt;
            var tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
            tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            tex.Apply();

            // Restore
            targetCamera.targetTexture = oldRT;
            targetCamera.clearFlags = oldClearFlags;
            targetCamera.backgroundColor = oldBGColor;
            targetCamera.forceIntoRenderTexture = oldForceIntoRT;

#if REX_URP
            var camDataRestor = targetCamera.GetUniversalAdditionalCameraData();
            if (camDataRestor != null)
            {
                camDataRestor.renderPostProcessing = oldPost;
            }
#endif
            RenderTexture.active = null;

            rt.Release();
            DestroyImmediate(rt);

            byte[] bytes = tex.EncodeToPNG();
            DestroyImmediate(tex);

            string fileName = $"QuickShot_{DateTime.Now:yyyyMMdd_HHmmss}.png";
            string fullPath = Path.Combine(exportPath, fileName);
            File.WriteAllBytes(fullPath, bytes);

            Debug.Log($"[RexTools] Screenshot saved to: {fullPath}");
            if (autoReveal) EditorUtility.RevealInFinder(fullPath);

            if (autoCopy)
            {
                CopyToClipboard(fullPath);
            }
        }

        private void CopyToClipboard(string path)
        {
            if (Application.platform == RuntimePlatform.WindowsEditor)
            {
                try
                {
                    var formattedPath = path.Replace("/", "\\").Replace("'", "''");
                    var process = new System.Diagnostics.Process();
                    process.StartInfo.FileName = "powershell.exe";
                    process.StartInfo.Arguments = $"-NoProfile -Command \"Add-Type -AssemblyName System.Windows.Forms; Add-Type -AssemblyName System.Drawing; [System.Windows.Forms.Clipboard]::SetImage([System.Drawing.Image]::FromFile('{formattedPath}'))\"";
                    process.StartInfo.CreateNoWindow = true;
                    process.StartInfo.UseShellExecute = false;
                    process.Start();
                }
                catch (Exception e)
                {
                    Debug.LogError($"[RexTools] Failed to copy image to Windows clipboard: {e.Message}");
                }
            }
            else if (Application.platform == RuntimePlatform.OSXEditor)
            {
                try
                {
                    var process = new System.Diagnostics.Process();
                    process.StartInfo.FileName = "osascript";
                    process.StartInfo.Arguments = $"-e \"set the clipboard to (read (POSIX file \\\"{path}\\\") as «class PNGf»)\"";
                    process.StartInfo.CreateNoWindow = true;
                    process.StartInfo.UseShellExecute = false;
                    process.Start();
                }
                catch (Exception e)
                {
                    Debug.LogError($"[RexTools] Failed to copy image to macOS clipboard: {e.Message}");
                }
            }
            else
            {
                Debug.LogWarning("[RexTools] Clipboard copy is only supported on Windows and macOS editors.");
            }
        }

        private enum ShotMode { Scene, Game }
    }
}
