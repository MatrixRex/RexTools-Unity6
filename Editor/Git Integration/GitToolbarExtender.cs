using System;
using System.Reflection;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace RexTools.GitIntegration.Editor
{
    /// <summary>
    /// Injects a Git branch label and status indicator into the PlayMode zone of the main editor toolbar.
    /// </summary>
    [InitializeOnLoad]
    public static class GitToolbarExtender
    {
        private static string cachedBranchName = "--";
        private static int cachedAhead = 0;
        private static int cachedBehind = 0;
        private static int cachedModified = 0;
        private static double lastGitUpdateTime = 0.0;
        private const double GitUpdateInterval = 60.0; // 60 seconds

        private static double lastInjectionCheck = 0.0;
        private const double InjectionCheckInterval = 2.0; // Check layout every 2 seconds

        static GitToolbarExtender()
        {
            EditorApplication.delayCall += Initialize;
        }

        private static void Initialize()
        {
            EditorApplication.update += OnEditorUpdate;
            EditorApplication.focusChanged += OnFocusChanged;
            
            // Initial check
            CheckAndInject(true);
        }

        private static void OnFocusChanged(bool focused)
        {
            if (focused)
            {
                ForceRefresh();
            }
        }

        private static void OnEditorUpdate()
        {
            double now = EditorApplication.timeSinceStartup;
            if (now - lastInjectionCheck > InjectionCheckInterval)
            {
                lastInjectionCheck = now;
                CheckAndInject(false);
            }
        }

        /// <summary>
        /// Resets the git check timer to trigger a fresh git CLI query next check.
        /// </summary>
        public static void ForceRefresh()
        {
            lastGitUpdateTime = 0.0;
            CheckAndInject(true);
        }

        private static void CheckAndInject(bool forceGitUpdate)
        {
            try
            {
                // Retrieve internal Toolbar type
                Type toolbarType = typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.Toolbar");
                if (toolbarType == null) return;

                var toolbars = Resources.FindObjectsOfTypeAll(toolbarType);
                if (toolbars == null || toolbars.Length == 0) return;

                var toolbar = toolbars[0];
                var rootField = toolbarType.GetField("m_Root", BindingFlags.NonPublic | BindingFlags.Instance);
                var root = rootField?.GetValue(toolbar) as VisualElement;
                if (root == null) return;

                // Find playmode buttons zone
                var playZone = root.Q("ToolbarZonePlayMode");
                if (playZone == null) return;

                var btn = playZone.Q<Label>("rex-git-toolbar-btn");
                if (btn == null)
                {
                    btn = new Label();
                    btn.name = "rex-git-toolbar-btn";
                    btn.style.unityFontStyleAndWeight = FontStyle.Bold;
                    btn.style.fontSize = 11;
                    btn.style.paddingLeft = 8;
                    btn.style.paddingRight = 8;
                    btn.style.marginLeft = 6;
                    btn.style.marginRight = 6;
                    btn.style.height = 20;
                    btn.style.alignSelf = Align.Center;
                    btn.style.unityTextAlign = TextAnchor.MiddleCenter;

                    btn.RegisterCallback<MouseOverEvent>(evt => btn.style.backgroundColor = new Color(1, 1, 1, 0.1f));
                    btn.RegisterCallback<MouseOutEvent>(evt => btn.style.backgroundColor = Color.clear);
                    
                    btn.RegisterCallback<ClickEvent>(evt => GitIntegrationWindow.ShowWindow());

                    playZone.Add(btn);
                    
                    _ = UpdateBranchLabelAsync(btn, true);
                }
                else
                {
                    _ = UpdateBranchLabelAsync(btn, forceGitUpdate);
                }
            }
            catch
            {
                // Fail silently to prevent crashing the editor if Unity alters their toolbar implementation
            }
        }

        private static async Task UpdateBranchLabelAsync(Label btn, bool force)
        {
            if (btn == null) return;

            double now = EditorApplication.timeSinceStartup;
            if (force || now - lastGitUpdateTime > GitUpdateInterval || cachedBranchName == "--")
            {
                lastGitUpdateTime = now;
                cachedBranchName = await GitRunner.GetCurrentBranchAsync();
                
                if (GitRunner.HasGitRepository())
                {
                    (cachedAhead, cachedBehind) = await GitRunner.GetSyncCountsAsync();
                    cachedModified = await GitRunner.GetModifiedFilesCountAsync();
                }
                else
                {
                    cachedAhead = 0;
                    cachedBehind = 0;
                    cachedModified = 0;
                }
            }

            string text = $"Git: {cachedBranchName}";
            if (cachedAhead > 0 || cachedBehind > 0 || cachedModified > 0)
            {
                text += " *";
            }
            btn.text = text;

            string tooltip = $"Branch: {cachedBranchName}\n";
            if (GitRunner.HasGitRepository())
            {
                tooltip += $"Ahead: {cachedAhead} commits\nBehind: {cachedBehind} commits\nModified files: {cachedModified}\nClick to open Git Integration Window";
                
                // Color based on theme skin
                if (EditorGUIUtility.isProSkin)
                    btn.style.color = new Color(0.4f, 0.8f, 1.0f); // Sleek cyan/blue for dark skin
                else
                    btn.style.color = new Color(0.0f, 0.35f, 0.7f); // Rich dark blue for light skin
            }
            else
            {
                tooltip += "No Git repository found.";
                
                if (EditorGUIUtility.isProSkin)
                    btn.style.color = new Color(0.7f, 0.7f, 0.7f); // Grey
                else
                    btn.style.color = new Color(0.2f, 0.2f, 0.2f); // Dark grey
            }
            btn.tooltip = tooltip;
        }
    }
}
