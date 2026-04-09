using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace RexTools.BatchMaterialEditor.Editor.Tabs
{
    public class ReplaceUI
    {
        public VisualElement Root { get; private set; }
        public ObjectField FindMatField { get; private set; }
        public ObjectField ReplaceMatField { get; private set; }
        public EnumField ModeField { get; private set; }
        public Button BtnScanReplace { get; private set; }
        public Button BtnConvert { get; private set; }
        public ScrollView AffectedListView { get; private set; }

        public ReplaceUI(VisualElement container)
        {
            // Load UXML
            string uxmlPath = "Editor/Batch Material Editor/Tabs/Replace/ReplaceTab.uxml";
            string fullPath = "Assets/" + uxmlPath;
            var visualTree = UnityEditor.AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(fullPath);
            
            if (visualTree == null)
            {
                fullPath = "Packages/com.matrixrex.rextools/" + uxmlPath;
                visualTree = UnityEditor.AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(fullPath);
            }

            if (visualTree != null)
            {
                Root = visualTree.CloneTree().ElementAt(0);
                FindMatField = Root.Q<ObjectField>("find-mat");
                FindMatField.objectType = typeof(UnityEngine.Material);
                
                ReplaceMatField = Root.Q<ObjectField>("replace-mat");
                ReplaceMatField.objectType = typeof(UnityEngine.Material);
                
                ModeField = Root.Q<EnumField>("mode");
                ModeField.Init(ReplaceMode.Scene);
                
                BtnScanReplace = Root.Q<Button>("btn-scan");
                BtnConvert = Root.Q<Button>("btn-convert");
                BtnConvert.SetEnabled(false);
                
                AffectedListView = Root.Q<ScrollView>("affected-list");
            }
            else
            {
                Root = new VisualElement();
                Root.Add(new Label("Could not load ReplaceTab.uxml"));
                FindMatField = new ObjectField();
                ReplaceMatField = new ObjectField();
                ModeField = new EnumField();
                BtnScanReplace = new Button();
                BtnConvert = new Button();
                AffectedListView = new ScrollView();
            }

            container.Add(Root);
        }

        public void PopulateAffectedList(System.Collections.Generic.List<UnityEngine.Object> affectedObjects, System.Action<int> onRemove)
        {
            AffectedListView.Clear();
            if (affectedObjects.Count == 0) 
            { 
                AffectedListView.Add(new Label("No assets affected.") { style = { marginTop = 20, alignSelf = Align.Center } }); 
                return; 
            }
            for (int i = 0; i < affectedObjects.Count; i++) {
                int index = i;
                var row = new VisualElement(); 
                row.AddToClassList("rex-result-item");
                
                var field = new ObjectField { value = affectedObjects[index], objectType = typeof(UnityEngine.GameObject), style = { flexGrow = 1 } }; 
                field.SetEnabled(false);
                
                var rmvBtn = new Button(() => onRemove(index)) { text = "-" }; 
                rmvBtn.AddToClassList("rex-button-small");
                
                row.Add(field); 
                row.Add(rmvBtn); 
                AffectedListView.Add(row);
            }
        }
    }
}
