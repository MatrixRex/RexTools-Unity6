using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace RexTools.BatchMaterialEditor.Editor.Tabs
{
    public class ReplacerUI
    {
        public VisualElement Root { get; private set; }
        public ObjectField FindMatField { get; private set; }
        public ObjectField ReplaceMatField { get; private set; }
        public EnumField ModeField { get; private set; }
        public Button BtnScanReplace { get; private set; }
        public Button BtnConvert { get; private set; }
        public ScrollView AffectedListView { get; private set; }

        public VisualElement SearchModeSection { get; private set; }
        public VisualElement ManualModeSection { get; private set; }
        public VisualElement ManualMatList { get; private set; }
        public Button BtnAddManualMat { get; private set; }
        public Button BtnCollapseManual { get; private set; }
        public Button BtnCollapseAffected { get; private set; }

        public ReplacerUI(VisualElement container)
        {
            // Load UXML
            string uxmlPath = "Editor/Batch Material Editor/Tabs/Replace/ReplacerTab.uxml";
            string fullPath = "Assets/" + uxmlPath;
            var visualTree = UnityEditor.AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(fullPath);
            
            if (visualTree == null)
            {
                fullPath = "Packages/com.matrixrex.rextools/" + uxmlPath;
                visualTree = UnityEditor.AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(fullPath);
            }

            if (visualTree != null)
            {
                Root = visualTree.CloneTree().Q<VisualElement>("replacer-tab-root");
                FindMatField = Root.Q<ObjectField>("find-mat");
                FindMatField.objectType = typeof(UnityEngine.Material);
                
                ReplaceMatField = Root.Q<ObjectField>("replace-mat");
                ReplaceMatField.objectType = typeof(UnityEngine.Material);
                
                ModeField = Root.Q<EnumField>("mode");
                BtnScanReplace = Root.Q<Button>("btn-scan");
                BtnConvert = Root.Q<Button>("btn-convert");
                BtnConvert.SetEnabled(false);

                AffectedListView = Root.Q<ScrollView>("affected-list");

                SearchModeSection = Root.Q<VisualElement>("search-mode-section");
                ManualModeSection = Root.Q<VisualElement>("manual-mode-section");
                ManualMatList = Root.Q<VisualElement>("manual-mat-list");
                BtnAddManualMat = Root.Q<Button>("btn-add-manual-mat");
                BtnCollapseManual = Root.Q<Button>("btn-collapse-manual");
                BtnCollapseAffected = Root.Q<Button>("btn-collapse-affected");

                ModeField.Init(ReplaceMode.Search);
            }
            else
            {
                Root = new ScrollView();
                Root.Add(new Label("Could not load ReplacerTab.uxml"));
                FindMatField = new ObjectField();
                ReplaceMatField = new ObjectField();
                ModeField = new EnumField();
                BtnScanReplace = new Button();
                BtnConvert = new Button();
                AffectedListView = new ScrollView();
                SearchModeSection = new VisualElement();
                ManualModeSection = new VisualElement();
                ManualMatList = new VisualElement();
                BtnAddManualMat = new Button();
                BtnCollapseManual = new Button();
                BtnCollapseAffected = new Button();
            }

            container.Add(Root);
        }

        public void PopulateManualMaterials(System.Collections.Generic.List<UnityEngine.Material> manualMats, System.Action<int> onRemove)
        {
            ManualMatList.Clear();
            if (manualMats.Count == 0)
            {
                ManualMatList.Add(new Label("Drop materials or objects here.") { style = { marginTop = 10, alignSelf = Align.Center, opacity = 0.5f } });
                return;
            }

            for (int i = 0; i < manualMats.Count; i++)
            {
                int index = i;
                var row = new VisualElement();
                row.AddToClassList("rex-row");
                row.style.marginBottom = 2;

                var field = new ObjectField { value = manualMats[index], objectType = typeof(UnityEngine.Material), style = { flexGrow = 1 } };
                field.RegisterValueChangedCallback(evt => manualMats[index] = (UnityEngine.Material)evt.newValue);

                var rmvBtn = new Button(() => onRemove(index)) { text = "-" };
                rmvBtn.AddToClassList("rex-button-small");

                row.Add(field);
                row.Add(rmvBtn);
                ManualMatList.Add(row);
            }
        }

        public void PopulateAffectedList(System.Collections.Generic.Dictionary<UnityEngine.Material, System.Collections.Generic.List<UnityEngine.GameObject>> groupedObjects, System.Action<UnityEngine.Material> onRemoveGroup)
        {
            AffectedListView.Clear();
            if (groupedObjects.Count == 0)
            {
                AffectedListView.Add(new Label("No assets affected.") { style = { marginTop = 20, alignSelf = Align.Center } });
                return;
            }

            foreach (var kvp in groupedObjects)
            {
                var mat = kvp.Key;
                var objects = kvp.Value;

                // Material Header
                var matHeader = new VisualElement { name = "grouped-mat-header" };
                matHeader.style.flexDirection = FlexDirection.Row;
                matHeader.style.backgroundColor = new Color(0.25f, 0.25f, 0.25f, 1f);
                matHeader.style.paddingLeft = 5;
                matHeader.style.paddingTop = 2;
                matHeader.style.paddingBottom = 2;
                matHeader.style.marginTop = 5;
                matHeader.style.alignItems = Align.Center;

                var matField = new ObjectField { value = mat, objectType = typeof(UnityEngine.Material), style = { flexGrow = 1 } };
                matField.SetEnabled(false);
                matHeader.Add(matField);
                matHeader.Add(new Label($" ({objects.Count})") { style = { unityTextAlign = TextAnchor.MiddleLeft, marginLeft = 5, color = Color.gray, marginRight = 5 } });

                var rmvGroupBtn = new Button(() => onRemoveGroup(mat)) { text = "×" };
                rmvGroupBtn.AddToClassList("rex-button-small");
                rmvGroupBtn.style.color = Color.red;
                matHeader.Add(rmvGroupBtn);

                AffectedListView.Add(matHeader);

                // Objects under this material
                for (int i = 0; i < objects.Count; i++)
                {
                    var row = new VisualElement();
                    row.AddToClassList("rex-result-item");
                    row.style.marginLeft = 15;
                    row.style.borderLeftWidth = 1;
                    row.style.borderLeftColor = Color.gray;

                    var field = new ObjectField { value = objects[i], objectType = typeof(UnityEngine.GameObject), style = { flexGrow = 1 } };
                    field.SetEnabled(false);

                    row.Add(field);
                    AffectedListView.Add(row);
                }
            }
        }
    }
}
