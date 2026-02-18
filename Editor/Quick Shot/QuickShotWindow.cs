using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEditor.UIElements;
using System.IO;
using System.Linq;
using System;
#if REX_URP
using UnityEngine.Rendering.Universal;
#endif

namespace RexTools.QuickShot.Editor
{
    public class QuickShotWindow : EditorWindow
    {
        private string exportPath;
        private bool isSceneMode = true;
        private float renderScale = 1.0f;
        private bool transparentBG = false;
        private bool autoReveal = true;

        private TextField pathField;
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
            var pathBox = new VisualElement();
            pathBox.AddToClassList("rex-box");

            var pathFoldout = new Foldout();
            pathFoldout.text = "EXPORT PATH";
            pathFoldout.value = true;
            
            // Style foldout label to match sections
            var foldoutLabel = pathFoldout.Q<Label>();
            if (foldoutLabel != null) foldoutLabel.AddToClassList("rex-section-label");
            
            pathBox.Add(pathFoldout);

            var pathRow = new VisualElement();
            pathRow.AddToClassList("rex-row");
            
            pathField = new TextField { value = exportPath };
            pathField.style.flexGrow = 1;
            pathField.style.flexShrink = 1; // Allow shrinking
            pathField.style.minWidth = 50;   // But not too much
            pathField.RegisterValueChangedCallback(e => exportPath = e.newValue);
            
            // Drag and Drop
            pathField.RegisterCallback<DragUpdatedEvent>(e => DragAndDrop.visualMode = DragAndDropVisualMode.Copy);
            pathField.RegisterCallback<DragPerformEvent>(e => {
                DragAndDrop.AcceptDrag();
                string path = DragAndDrop.paths.FirstOrDefault();
                if (!string.IsNullOrEmpty(path)) {
                    if (Directory.Exists(path)) {
                        exportPath = path.Replace("\\", "/");
                        pathField.value = exportPath;
                    } else if (File.Exists(path)) {
                        exportPath = Path.GetDirectoryName(path).Replace("\\", "/");
                        pathField.value = exportPath;
                    }
                }
            });

            pathRow.Add(pathField);

            var folderBtn = new Button();
            folderBtn.text = "";
            folderBtn.style.width = 24;
            folderBtn.style.height = 20;
            folderBtn.style.backgroundImage = (Texture2D)EditorGUIUtility.IconContent("Folder Icon").image;
            folderBtn.tooltip = "Select Export Folder";
            folderBtn.clicked += () => {
                string selectedPath = EditorUtility.OpenFolderPanel("Select Export Folder", exportPath, "");
                if (!string.IsNullOrEmpty(selectedPath)) {
                    exportPath = selectedPath;
                    pathField.value = exportPath;
                }
            };
            pathRow.Add(folderBtn);

            pathFoldout.Add(pathRow);

            var folderOpsRow = new VisualElement();
            folderOpsRow.AddToClassList("rex-row");
            folderOpsRow.style.marginTop = 2;

            var openBtn = new Button();
            openBtn.text = "Open Folder";
            openBtn.style.height = 20;
            openBtn.style.paddingLeft = 8;
            openBtn.style.paddingRight = 8;
            openBtn.tooltip = "Open Folder in Explorer";
            openBtn.clicked += () => {
                if (Directory.Exists(exportPath)) EditorUtility.RevealInFinder(exportPath);
                else Debug.LogWarning($"[RexTools] Export path does not exist: {exportPath}");
            };
            folderOpsRow.Add(openBtn);

            var autoOpenBtn = new Button();
            autoOpenBtn.text = $"Auto Open: {(autoReveal ? "ON" : "OFF")}";
            autoOpenBtn.style.height = 20;
            autoOpenBtn.style.marginLeft = 4;
            autoOpenBtn.style.paddingLeft = 8;
            autoOpenBtn.style.paddingRight = 8;
            if (autoReveal) autoOpenBtn.AddToClassList("rex-toggle-on");
            
            autoOpenBtn.clicked += () => {
                autoReveal = !autoReveal;
                autoOpenBtn.text = $"Auto Open: {(autoReveal ? "ON" : "OFF")}";
                if (autoReveal) autoOpenBtn.AddToClassList("rex-toggle-on");
                else autoOpenBtn.RemoveFromClassList("rex-toggle-on");
            };
            folderOpsRow.Add(autoOpenBtn);

            pathFoldout.Add(folderOpsRow);
            
            root.Add(pathBox);

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
            renderScaleContainer.AddToClassList("rex-row");
            renderScaleContainer.style.display = isSceneMode ? DisplayStyle.None : DisplayStyle.Flex;
            
            var scaleSlider = new Slider("Render Scale", 1f, 8f) { value = renderScale, showInputField = true };
            scaleSlider.AddToClassList("rex-flex-grow");
            scaleSlider.RegisterValueChangedCallback(e => renderScale = e.newValue);
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
        }

        private enum ShotMode { Scene, Game }
    }
}
