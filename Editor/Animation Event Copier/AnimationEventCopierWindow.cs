using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEditor.UIElements;
using System.Linq;
using System.Collections.Generic;
using RexTools.Editor.Core;

namespace RexTools.Animation
{
    public class AnimationEventCopierWindow : EditorWindow
    {
        [SerializeField]
        private VisualTreeAsset m_VisualTreeAsset = default;

        private GameObject sourceModel;
        private GameObject targetModel;

        private ModelImporterClipAnimation[] sourceClips;
        private ModelImporterClipAnimation[] targetClips;
        private List<string> sourceClipNames;
        private int[] clipMapping;

        private VisualElement mappingSection;
        private VisualElement mappingList;
        private Button btnCopy;
        
        private RexHelpBox helpBox;
        private bool showHelp = false;

        [MenuItem("Tools/Rex Tools/Animation Event Copier")]
        public static void ShowWindow()
        {
            AnimationEventCopierWindow wnd = GetWindow<AnimationEventCopierWindow>();
            wnd.titleContent = new GUIContent("Animation Event Copier");
            wnd.minSize = new Vector2(400, 450);
        }

        public void CreateGUI()
        {
            VisualElement root = rootVisualElement;

            // Load UXML
            var visualTree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/RexTools/Editor/Animation Event Copier/AnimationEventCopier.uxml");
            if (visualTree == null)
            {
                // Try finding it dynamically if path changes
                string[] guids = AssetDatabase.FindAssets("AnimationEventCopier t:VisualTreeAsset");
                if (guids.Length > 0)
                    visualTree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(AssetDatabase.GUIDToAssetPath(guids[0]));
            }

            if (visualTree != null)
            {
                visualTree.CloneTree(root);
            }
            else
            {
                root.Add(new Label("Failed to load UXML."));
                return;
            }

            // Load Styles
            var styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>("Assets/RexTools/Editor/RexToolsStyles.uss");
            if (styleSheet == null)
            {
                string[] guids = AssetDatabase.FindAssets("RexToolsStyles t:StyleSheet");
                if (guids.Length > 0)
                    styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(AssetDatabase.GUIDToAssetPath(guids[0]));
            }

            if (styleSheet != null)
            {
                root.styleSheets.Add(styleSheet);
            }

            // Bind Elements
            var sourceField = root.Q<ObjectField>("source-model");
            var targetField = root.Q<ObjectField>("target-model");

            mappingSection = root.Q<VisualElement>("mapping-section");
            mappingList = root.Q<VisualElement>("mapping-list");

            var btnAutoMatch = root.Q<Button>("btn-auto-match");
            btnCopy = root.Q<Button>("btn-copy");
            
            // --- BRANDED HEADER & HELP BOX ---
            var helpBoxContainer = root.Q<VisualElement>("help-box-container");
            if (helpBoxContainer != null)
            {
                helpBox = new RexHelpBox("Copy animation events from a source FBX to a target FBX. Map specific clips together if their names do not match perfectly.");
                helpBoxContainer.Add(helpBox);
            }

            var headerContainer = root.Q<VisualElement>("header-container");
            if (headerContainer != null)
            {
                var header = new RexHeader("Animation Event Copier", showHelpButton: true);
                header.OnHelpClicked += () => {
                    showHelp = !showHelp;
                    helpBox?.ToggleVisibility();
                    header.SetHelpButtonActive(showHelp);
                };
                headerContainer.Add(header);
            }

            sourceField.RegisterValueChangedCallback(evt => {
                sourceModel = evt.newValue as GameObject;
                RefreshClips();
            });

            targetField.RegisterValueChangedCallback(evt => {
                targetModel = evt.newValue as GameObject;
                RefreshClips();
            });

            btnAutoMatch.clicked += AutoMatch;
            btnCopy.clicked += CopyEvents;

            UpdateButtonState();
        }

        private void RefreshClips()
        {
            sourceClips = null;
            targetClips = null;
            sourceClipNames = null;
            clipMapping = null;
            mappingList.Clear();

            if (sourceModel == null || targetModel == null)
            {
                mappingSection.AddToClassList("rex-hidden");
                UpdateButtonState();
                return;
            }

            string sourcePath = AssetDatabase.GetAssetPath(sourceModel);
            string targetPath = AssetDatabase.GetAssetPath(targetModel);

            ModelImporter sourceImporter = AssetImporter.GetAtPath(sourcePath) as ModelImporter;
            ModelImporter targetImporter = AssetImporter.GetAtPath(targetPath) as ModelImporter;

            if (sourceImporter == null || targetImporter == null)
            {
                mappingSection.AddToClassList("rex-hidden");
                UpdateButtonState();
                return;
            }

            sourceClips = sourceImporter.clipAnimations.Length > 0 ? sourceImporter.clipAnimations : sourceImporter.defaultClipAnimations;
            targetClips = targetImporter.clipAnimations.Length > 0 ? targetImporter.clipAnimations : targetImporter.defaultClipAnimations;

            if (sourceClips != null && targetClips != null && sourceClips.Length > 0 && targetClips.Length > 0)
            {
                sourceClipNames = new List<string> { "None" };
                sourceClipNames.AddRange(sourceClips.Select(c => c.name));

                clipMapping = new int[targetClips.Length];
                
                mappingSection.RemoveFromClassList("rex-hidden");
                
                RebuildMappingUI();
                AutoMatch();
            }
            else
            {
                mappingSection.AddToClassList("rex-hidden");
            }

            UpdateButtonState();
        }

        private void RebuildMappingUI()
        {
            mappingList.Clear();

            for (int i = 0; i < targetClips.Length; i++)
            {
                int index = i; // local copy for closure

                var row = new VisualElement();
                row.AddToClassList("rex-row");

                var targetLabel = new Label(targetClips[index].name);
                targetLabel.style.flexGrow = 1;
                targetLabel.style.flexBasis = new StyleLength(Length.Percent(50));
                
                var dropdown = new DropdownField(sourceClipNames, 0);
                dropdown.style.flexGrow = 1;
                dropdown.style.flexBasis = new StyleLength(Length.Percent(50));
                dropdown.RegisterValueChangedCallback(evt => {
                    // index in dropdown is mapped to clipMapping + 1
                    clipMapping[index] = sourceClipNames.IndexOf(evt.newValue) - 1;
                });

                row.Add(targetLabel);
                row.Add(dropdown);
                mappingList.Add(row);
            }
        }

        private void AutoMatch()
        {
            if (targetClips == null || sourceClips == null) return;

            var dropdowns = mappingList.Query<DropdownField>().ToList();

            for (int i = 0; i < targetClips.Length; i++)
            {
                clipMapping[i] = -1; // None
                dropdowns[i].index = 0;

                for (int j = 0; j < sourceClips.Length; j++)
                {
                    if (targetClips[i].name == sourceClips[j].name)
                    {
                        clipMapping[i] = j;
                        dropdowns[i].index = j + 1;
                        break;
                    }
                }
            }
        }

        private void UpdateButtonState()
        {
            if (sourceClips != null && targetClips != null && sourceClips.Length > 0 && targetClips.Length > 0)
            {
                btnCopy.SetEnabled(true);
            }
            else
            {
                btnCopy.SetEnabled(false);
            }
        }

        private void CopyEvents()
        {
            if (targetModel == null) return;

            string targetPath = AssetDatabase.GetAssetPath(targetModel);
            ModelImporter targetImporter = AssetImporter.GetAtPath(targetPath) as ModelImporter;

            if (targetImporter == null) return;

            bool modified = false;

            for (int i = 0; i < targetClips.Length; i++)
            {
                int sourceIndex = clipMapping[i];
                if (sourceIndex >= 0)
                {
                    var sourceClip = sourceClips[sourceIndex];
                    if (sourceClip.events != null && sourceClip.events.Length > 0)
                    {
                        targetClips[i].events = sourceClip.events;
                        modified = true;
                        Debug.Log($"[AnimationEventCopier] Copied {sourceClip.events.Length} events from '{sourceClip.name}' to '{targetClips[i].name}'.");
                    }
                }
            }

            if (modified)
            {
                targetImporter.clipAnimations = targetClips;
                targetImporter.SaveAndReimport();
                EditorUtility.DisplayDialog("Success", "Successfully copied animation events and reimported the target model.", "OK");
            }
            else
            {
                EditorUtility.DisplayDialog("Notice", "No events were copied. The selected source clips might not have any events.", "OK");
            }
        }
    }
}
