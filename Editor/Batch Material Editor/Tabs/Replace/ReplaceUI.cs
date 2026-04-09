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
            Root = new VisualElement { style = { flexGrow = 1, display = DisplayStyle.None } };

            var findReplaceBox = new VisualElement();
            findReplaceBox.AddToClassList("rex-box");
            
            var findLabel = new Label("SEARCH & REPLACE");
            findLabel.AddToClassList("rex-section-label");
            findReplaceBox.Add(findLabel);

            FindMatField = new ObjectField("Find Material") { objectType = typeof(UnityEngine.Material) };
            findReplaceBox.Add(FindMatField);

            ReplaceMatField = new ObjectField("Replace Material") { objectType = typeof(UnityEngine.Material) };
            findReplaceBox.Add(ReplaceMatField);

            ModeField = new EnumField("Replacement Mode", ReplaceMode.Scene);
            findReplaceBox.Add(ModeField);
            
            Root.Add(findReplaceBox);

            BtnScanReplace = new Button { text = "1. SCAN AFFECTED OBJECTS" };
            BtnScanReplace.AddToClassList("rex-action-button"); 
            BtnScanReplace.AddToClassList("rex-action-button--pack");
            Root.Add(BtnScanReplace);

            var affectedBox = new VisualElement();
            affectedBox.AddToClassList("rex-box"); 
            affectedBox.style.flexGrow = 1;

            var affectedLabel = new Label("AFFECTED OBJECTS");
            affectedLabel.AddToClassList("rex-section-label"); 
            affectedBox.Add(affectedLabel);

            AffectedListView = new ScrollView(); 
            AffectedListView.AddToClassList("rex-result-list");
            affectedBox.Add(AffectedListView);
            Root.Add(affectedBox);

            BtnConvert = new Button { text = "2. START CONVERSION" };
            BtnConvert.AddToClassList("rex-action-button"); 
            BtnConvert.AddToClassList("rex-action-button--unpack");
            BtnConvert.SetEnabled(false);
            Root.Add(BtnConvert);

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
