using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace RexTools.Editor.Core
{
    public class RexHeader : VisualElement
    {
        public Button HelpButton { get; private set; }
        public event Action OnHelpClicked;

        public RexHeader(string toolTitle, bool showHelpButton = false)
        {
            AddToClassList("rex-header-row");

            var brandStack = new VisualElement();
            brandStack.AddToClassList("rex-header-stack");

            var brandLabel = new Label("Rex Tools");
            brandLabel.AddToClassList("rex-brand-label");
            brandStack.Add(brandLabel);

            var titleLabel = new Label(toolTitle);
            titleLabel.AddToClassList("rex-tool-title");
            brandStack.Add(titleLabel);

            Add(brandStack);

            if (showHelpButton)
            {
                HelpButton = new Button();
                HelpButton.AddToClassList("rex-help-btn");
                HelpButton.clicked += () => OnHelpClicked?.Invoke();
                Add(HelpButton);
            }
        }

        public void SetHelpButtonActive(bool active)
        {
            if (HelpButton != null)
            {
                if (active) HelpButton.AddToClassList("rex-help-btn--active");
                else HelpButton.RemoveFromClassList("rex-help-btn--active");
            }
        }
    }
}
