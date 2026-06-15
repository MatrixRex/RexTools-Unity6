using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

namespace RexTools.ScenePack.Editor
{
    [CreateAssetMenu(fileName = "New Scene Pack", menuName = "Rex Tools/Scene Pack", order = 100)]
    [Icon("Packages/com.matrixrex.rextools/Editor/Icons/scenepack.png")]
    public class ScenePack : ScriptableObject
    {
        [Tooltip("List of scene assets to open when double-clicking or clicking Open Scene Pack.")]
        public List<SceneAsset> scenes = new List<SceneAsset>();

        /// <summary>
        /// Opens all scenes in the scene pack.
        /// </summary>
        /// <param name="additive">If true, opens all scenes additively. If false, opens the first scene as Single and the rest additively.</param>
        public void OpenScenes(bool additive = false)
        {
            if (scenes == null || scenes.Count == 0)
            {
                EditorUtility.DisplayDialog("Open Scene Pack", "No scenes configured in this Scene Pack.", "OK");
                return;
            }

            // Prompt user to save changes to currently open scenes
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                // User cancelled the save prompt
                return;
            }

            int openedCount = 0;
            bool isFirst = true;

            foreach (var sceneAsset in scenes)
            {
                if (sceneAsset == null) continue;

                string scenePath = AssetDatabase.GetAssetPath(sceneAsset);
                if (string.IsNullOrEmpty(scenePath)) continue;

                OpenSceneMode mode = OpenSceneMode.Additive;
                if (!additive && isFirst)
                {
                    mode = OpenSceneMode.Single;
                    isFirst = false;
                }

                EditorSceneManager.OpenScene(scenePath, mode);
                openedCount++;
            }

            if (openedCount > 0)
            {
                Debug.Log($"[Scene Pack] Successfully opened {openedCount} scenes from '{name}'.");
            }
            else
            {
                EditorUtility.DisplayDialog("Open Scene Pack", "Could not load any scenes from this Scene Pack. Please make sure the scenes are valid.", "OK");
            }
        }
    }
}
