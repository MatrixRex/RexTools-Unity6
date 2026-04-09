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

        public ScannerUI(VisualElement container)
        {
            Root = new VisualElement { style = { flexGrow = 1 } };
            
            BtnScan = new Button { text = "SCAN SCENE MATERIALS" };
            BtnScan.AddToClassList("rex-action-button");
            BtnScan.AddToClassList("rex-action-button--pack");
            Root.Add(BtnScan);

            BtnCreateGroupFromSelection = new Button
            {
                text = "CREATE GROUP FROM SELECTION",
                style = { display = DisplayStyle.None }
            };
            BtnCreateGroupFromSelection.AddToClassList("rex-action-button");
            BtnCreateGroupFromSelection.AddToClassList("rex-action-button--unpack");
            Root.Add(BtnCreateGroupFromSelection);

            ScannerList = new ScrollView { style = { flexGrow = 1 } };
            ScannerList.AddToClassList("rex-box");
            ScannerList.AddToClassList("rex-result-list");
            Root.Add(ScannerList);

            container.Add(Root);
        }
    }
}
