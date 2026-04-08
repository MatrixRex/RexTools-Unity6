using UnityEditor;
using UnityEditor.Presets;
using UnityEngine;
using UnityEngine.UIElements;
using System.IO;

namespace RexTools.Editor.Core
{
    /// <summary>
    /// Reusable module for adding Unity Preset Save/Load functionality to Rex Tools.
    /// </summary>
    public static class RexPresetManager
    {
        /// <summary>
        /// Creates a row of buttons for saving and loading presets for a target object.
        /// </summary>
        /// <param name="target">The object to save/load presets for (usually a ScriptableObject).</param>
        /// <param name="presetName">The default name for the preset file.</param>
        /// <returns>A VisualElement containing the preset buttons.</returns>
        public static VisualElement CreatePresetButtons(Object target, string presetName = "NewPreset")
        {
            var container = new VisualElement();
            container.AddToClassList("rex-preset-container");

            // SAVE BUTTON
            var btnSave = new Button(() => SavePreset(target, presetName))
            {
                text = "SAVE PRESET",
                tooltip = "Save current settings as a new Unity Preset (.preset)"
            };
            btnSave.AddToClassList("rex-button-small");
            btnSave.AddToClassList("rex-preset-button");
            btnSave.style.flexGrow = 1;

            // LOAD/SELECTOR BUTTON
            var btnPresets = new Button(() => ShowPresetSelector(target))
            {
                text = "PRESETS",
                tooltip = "Open Unity Preset Selector for this tool"
            };
            btnPresets.AddToClassList("rex-button-small");
            btnPresets.AddToClassList("rex-preset-button");
            btnPresets.style.flexGrow = 1;

            container.Add(btnSave);
            container.Add(btnPresets);

            return container;
        }

        /// <summary>
        /// Creates a simple icon button that opens the Unity Preset Selector.
        /// </summary>
        public static VisualElement CreatePresetIconButton(Object target, string tooltip = "Load Preset")
        {
            var btn = new Button(() => ShowPresetSelector(target))
            {
                tooltip = tooltip
            };
            btn.AddToClassList("rex-preset-icon-btn");
            return btn;
        }

        public static void SavePreset(Object target, string defaultName)
        {
            if (target == null) return;

            string path = EditorUtility.SaveFilePanelInProject(
                "Save Preset",
                defaultName,
                "preset",
                "Save current settings as a Unity Preset asset."
            );

            if (string.IsNullOrEmpty(path)) return;

            // Create a new Preset from the current state of the target
            Preset preset = new Preset(target);
            
            // If the file already exists, we should probably update it or let Unity handle it
            AssetDatabase.CreateAsset(preset, path);
            AssetDatabase.SaveAssets();
            
            Debug.Log($"[RexTools] Preset saved to: {path}");
            EditorGUIUtility.PingObject(preset);
        }

        public static void ShowPresetSelector(Object target)
        {
            if (target == null) return;
            
            // Show the standard Unity Preset Selector
            // It will automatically apply any selected preset to the target object
            PresetSelector.ShowSelector(new Object[] { target }, null, true);
        }
    }
}
