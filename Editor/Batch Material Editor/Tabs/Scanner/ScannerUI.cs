using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace RexTools.BatchMaterialEditor.Editor.Tabs
{
    public class ScannerUI
    {
        public VisualElement Root { get; private set; }
        public Button BtnScan { get; private set; }
        public Button BtnCreateGroupFromSelection { get; private set; }
        public ScrollView ScannerList { get; private set; }
        public Label InfoLabel { get; private set; }

        public ScannerUI(VisualElement container)
        {
            // Load UXML
            string uxmlPath = "Editor/Batch Material Editor/Tabs/Scanner/ScannerTab.uxml";
            string fullPath = "Assets/" + uxmlPath;
            var visualTree = UnityEditor.AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(fullPath);
            
            if (visualTree == null)
            {
                fullPath = "Packages/com.matrixrex.rextools/" + uxmlPath;
                visualTree = UnityEditor.AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(fullPath);
            }

            if (visualTree != null)
            {
                Root = visualTree.CloneTree().Q<VisualElement>("scanner-tab-root");
                BtnScan = Root.Q<Button>("btn-scan");
                BtnCreateGroupFromSelection = Root.Q<Button>("btn-create-group");
                ScannerList = Root.Q<ScrollView>("scanner-list");
                InfoLabel = Root.Q<Label>("scanner-info");
            }
            else
            {
                Root = new VisualElement();
                Root.Add(new Label("Could not load ScannerTab.uxml"));
                BtnScan = new Button();
                BtnCreateGroupFromSelection = new Button();
                ScannerList = new ScrollView();
                InfoLabel = new Label();
            }

            container.Add(Root);
        }
    }
}
