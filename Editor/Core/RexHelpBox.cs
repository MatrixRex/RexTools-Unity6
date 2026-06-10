using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace RexTools.Editor.Core
{
    public class RexHelpBox : VisualElement
    {
        private readonly List<Label> items = new List<Label>();

        public RexHelpBox(params string[] lines)
        {
            AddToClassList("rex-box");
            AddToClassList("rex-help-box");
            AddToClassList("rex-hidden"); // Start collapsed by default

            var title = new Label("HOW TO USE:");
            title.AddToClassList("rex-help-text-title");
            Add(title);

            foreach (var line in lines)
            {
                AddHelpLine(line);
            }
        }

        public void AddHelpLine(string text)
        {
            var cleanText = "• " + text.TrimStart('•', ' ');
            var label = new Label(cleanText);
            label.AddToClassList("rex-help-text-item");
            Add(label);
            items.Add(label);
        }

        public void ToggleVisibility()
        {
            ToggleInClassList("rex-hidden");
        }

        public void SetVisible(bool visible)
        {
            if (visible) RemoveFromClassList("rex-hidden");
            else AddToClassList("rex-hidden");
        }

        public bool IsVisible => !ClassListContains("rex-hidden");
    }
}
