using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using RexTools.Editor.Core;

namespace RexTools.ScenePack.Editor
{
    [CustomEditor(typeof(ScenePack))]
    public class ScenePackEditor : UnityEditor.Editor
    {
        private bool showHelp = false;

        [OnOpenAsset(1)]
        public static bool OnOpenAsset(int instanceID, int line)
        {
            var obj = EditorUtility.InstanceIDToObject(instanceID);
            if (obj is ScenePack scenePack)
            {
                scenePack.OpenScenes(additive: false);
                return true; // Return true to indicate we handled opening the asset
            }
            return false; // Return false to let Unity handle it normally
        }

        public override VisualElement CreateInspectorGUI()
        {
            var root = new VisualElement();
            root.AddToClassList("rex-root-padding");

            // Load Global Styles
            string[] possibleStyles = {
                "Packages/com.matrixrex.rextools/Editor/RexToolsStyles.uss",
                "Assets/Editor/RexToolsStyles.uss"
            };
            StyleSheet globalStyleSheet = null;
            foreach (var path in possibleStyles)
            {
                globalStyleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(path);
                if (globalStyleSheet != null) break;
            }
            if (globalStyleSheet != null)
            {
                root.styleSheets.Add(globalStyleSheet);
            }

            // Header Container
            var headerContainer = new VisualElement();
            root.Add(headerContainer);

            // Help Box
            var helpBox = new RexHelpBox(
                "Add Scene Assets to the list below.",
                "Double-click this Scene Pack asset in your Project window to open all scenes.",
                "Or click the 'Open Scene Pack' buttons below to load the scenes."
            );
            root.Add(helpBox);

            // Header Instantiation
            var header = new RexHeader("Scene Pack", showHelpButton: true);
            header.OnHelpClicked += () =>
            {
                showHelp = !showHelp;
                helpBox.ToggleVisibility();
                header.SetHelpButtonActive(showHelp);
            };
            headerContainer.Add(header);

            // Content Area / Scene List
            var box = new VisualElement();
            box.AddToClassList("rex-box");

            var sectionLabel = new Label("SCENE PACK SETTINGS");
            sectionLabel.AddToClassList("rex-section-label");
            box.Add(sectionLabel);

            var scenesProp = serializedObject.FindProperty("scenes");
            var scenesField = new PropertyField(scenesProp);
            scenesField.Bind(serializedObject);
            scenesField.AddToClassList("rex-list-property-field");
            box.Add(scenesField);

            root.Add(box);

            // Actions Box
            var actionsBox = new VisualElement();
            actionsBox.AddToClassList("rex-box");

            var actionsLabel = new Label("ACTIONS");
            actionsLabel.AddToClassList("rex-section-label");
            actionsBox.Add(actionsLabel);

            // Button 1: Open Scenes (Replace)
            var openReplaceBtn = new Button();
            openReplaceBtn.text = "OPEN SCENE PACK (REPLACE)";
            openReplaceBtn.AddToClassList("rex-action-button");
            openReplaceBtn.AddToClassList("rex-action-button--pack");
            openReplaceBtn.style.height = 40; // Ensure it looks good as an action button
            openReplaceBtn.style.unityFontStyleAndWeight = FontStyle.Bold;
            openReplaceBtn.clicked += () =>
            {
                if (target is ScenePack scenePack)
                {
                    scenePack.OpenScenes(additive: false);
                }
            };
            actionsBox.Add(openReplaceBtn);

            // Button 2: Open Scenes (Additive)
            var openAdditiveBtn = new Button();
            openAdditiveBtn.text = "OPEN SCENE PACK (ADDITIVE)";
            openAdditiveBtn.AddToClassList("rex-button");
            openAdditiveBtn.style.marginTop = 8;
            openAdditiveBtn.style.height = 30;
            openAdditiveBtn.style.unityFontStyleAndWeight = FontStyle.Bold;
            openAdditiveBtn.clicked += () =>
            {
                if (target is ScenePack scenePack)
                {
                    scenePack.OpenScenes(additive: true);
                }
            };
            actionsBox.Add(openAdditiveBtn);

            root.Add(actionsBox);

            return root;
        }
    }
}
