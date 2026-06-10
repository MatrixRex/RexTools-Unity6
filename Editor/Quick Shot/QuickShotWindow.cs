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
        private bool isSceneMode = true;
        private float renderScale = 1.0f;
        private bool transparentBG = false;
        private bool autoReveal = true;
        private bool autoCopy = false;

        private RexFolderSelector folderSelector;
        private VisualElement renderScaleContainer;
        private VisualElement transparentToggleContainer;
        private Button captureButton;

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

            // --- BRANDED HEADER ---
            var header = new VisualElement();
            header.AddToClassList("rex-header-row");

            var brandStack = new VisualElement();
            brandStack.AddToClassList("rex-header-stack");

            var brandLabel = new Label("Rex Tools");
            brandLabel.AddToClassList("rex-brand-label");
            brandStack.Add(brandLabel);

            var titleLabel = new Label("Quick Shot");
            titleLabel.AddToClassList("rex-tool-title");
            brandStack.Add(titleLabel);

            header.Add(brandStack);
            root.Add(header);

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

            root.Add(exportBox);

            // --- SETTINGS ---
            var settingsBox = new VisualElement();
            settingsBox.AddToClassList("rex-box");

            var settingsLabel = new Label("SETTINGS");
            settingsLabel.AddToClassList("rex-section-label");
            settingsBox.Add(settingsLabel);

            // Mode Toggle
            var modeRow = new VisualElement();
            modeRow.AddToClassList("rex-row");
            var modeToggle = new EnumField("Mode", isSceneMode ? ShotMode.Scene : ShotMode.Game);
            modeToggle.AddToClassList("rex-flex-grow");
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

            var scaleSlider = new RexSlider(1f, 8f, defaultValue: 1f, value: renderScale, snapIncrement: 0.25f);
            scaleSlider.OnValueChanged += val => renderScale = val;
            renderScaleContainer.Add(scaleSlider);
            
            settingsBox.Add(renderScaleContainer);

            // Transparent BG
            transparentToggleContainer = new VisualElement();
            var transparentToggle = new Toggle("Transparent BG") { value = transparentBG };
            transparentToggle.RegisterValueChangedCallback(e => transparentBG = e.newValue);
            transparentToggleContainer.Add(transparentToggle);
            transparentToggleContainer.style.display = isSceneMode ? DisplayStyle.None : DisplayStyle.Flex;
            settingsBox.Add(transparentToggleContainer);

            root.Add(settingsBox);

            // --- POST OPERATIONS ---
            var postOpsBox = new VisualElement();
            postOpsBox.AddToClassList("rex-box");

            var postOpsLabel = new Label("POST OPERATIONS");
            postOpsLabel.AddToClassList("rex-section-label");
            postOpsBox.Add(postOpsLabel);

            var postOpsRow = new VisualElement();
            postOpsRow.AddToClassList("rex-row");

            var autoOpenBtn = new Button();
            autoOpenBtn.text = $"Auto Open: {(autoReveal ? "ON" : "OFF")}";
            autoOpenBtn.AddToClassList("rex-toggle-btn");
            autoOpenBtn.tooltip = "Reveal screenshot in Explorer/Finder after capture";
            if (autoReveal) autoOpenBtn.AddToClassList("rex-toggle-btn--active");
            
            autoOpenBtn.clicked += () => {
                autoReveal = !autoReveal;
                autoOpenBtn.text = $"Auto Open: {(autoReveal ? "ON" : "OFF")}";
                if (autoReveal) autoOpenBtn.AddToClassList("rex-toggle-btn--active");
                else autoOpenBtn.RemoveFromClassList("rex-toggle-btn--active");
            };
            postOpsRow.Add(autoOpenBtn);

            var autoCopyBtn = new Button();
            autoCopyBtn.text = $"Auto Copy: {(autoCopy ? "ON" : "OFF")}";
            autoCopyBtn.AddToClassList("rex-toggle-btn");
            autoCopyBtn.style.marginLeft = 4;
            autoCopyBtn.tooltip = "Copy screenshot to system clipboard after capture";
            if (autoCopy) autoCopyBtn.AddToClassList("rex-toggle-btn--active");

            autoCopyBtn.clicked += () => {
                autoCopy = !autoCopy;
                autoCopyBtn.text = $"Auto Copy: {(autoCopy ? "ON" : "OFF")}";
                if (autoCopy) autoCopyBtn.AddToClassList("rex-toggle-btn--active");
                else autoCopyBtn.RemoveFromClassList("rex-toggle-btn--active");
            };
            postOpsRow.Add(autoCopyBtn);

            postOpsBox.Add(postOpsRow);

            root.Add(postOpsBox);

            // --- CAPTURE BUTTON ---
            captureButton = new Button { text = "CAPTURE SCREENSHOT" };
            captureButton.AddToClassList("rex-action-button");
            captureButton.AddToClassList("rex-action-button--pack");
            captureButton.clicked += CaptureScreenshot;
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
