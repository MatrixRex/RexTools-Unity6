using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace RexTools.Editor.Core
{
    public class RexFoldout : VisualElement
    {
        private readonly Label titleLabel;
        private readonly Label countLabel;
        private readonly Label arrowLabel;
        private readonly VisualElement innerContentContainer;
        private bool isExpanded;

        public override VisualElement contentContainer => innerContentContainer;

        public bool IsExpanded
        {
            get => isExpanded;
            set
            {
                if (isExpanded != value)
                {
                    isExpanded = value;
                    innerContentContainer.style.display = isExpanded ? DisplayStyle.Flex : DisplayStyle.None;
                    arrowLabel.ToggleInClassList("rex-foldout-arrow--collapsed");
                    ToggleInClassList("rex-foldout--collapsed");
                    OnValueChanged?.Invoke(isExpanded);
                }
            }
        }

        public event Action<bool> OnValueChanged;

        public RexFoldout(string title, int? count = null, bool defaultExpanded = true)
        {
            AddToClassList("rex-foldout");

            // Header Container
            var header = new VisualElement();
            header.AddToClassList("rex-foldout-header");
            header.RegisterCallback<ClickEvent>(evt => IsExpanded = !IsExpanded);
            hierarchy.Add(header); // Use hierarchy.Add to bypass the contentContainer override

            // Title & Count Container
            var titleContainer = new VisualElement();
            titleContainer.AddToClassList("rex-foldout-title-container");
            header.Add(titleContainer);

            titleLabel = new Label(title);
            titleLabel.AddToClassList("rex-foldout-title");
            titleContainer.Add(titleLabel);

            countLabel = new Label();
            countLabel.AddToClassList("rex-foldout-count");
            titleContainer.Add(countLabel);
            SetCount(count);

            // Toggle arrow (using text symbol rotated in USS)
            arrowLabel = new Label("▼");
            arrowLabel.AddToClassList("rex-foldout-arrow");
            header.Add(arrowLabel);

            // Collapsible Content Wrapper
            innerContentContainer = new VisualElement();
            innerContentContainer.AddToClassList("rex-foldout-content");
            hierarchy.Add(innerContentContainer); // Use hierarchy.Add to bypass the contentContainer override

            // Set initial state
            isExpanded = defaultExpanded;
            innerContentContainer.style.display = isExpanded ? DisplayStyle.Flex : DisplayStyle.None;
            if (!isExpanded)
            {
                arrowLabel.AddToClassList("rex-foldout-arrow--collapsed");
                AddToClassList("rex-foldout--collapsed");
            }
        }

        public void SetTitle(string title)
        {
            titleLabel.text = title;
        }

        public void SetCount(int? count)
        {
            if (count.HasValue && count.Value >= 0)
            {
                countLabel.text = $"({count.Value})";
                countLabel.style.display = DisplayStyle.Flex;
            }
            else
            {
                countLabel.style.display = DisplayStyle.None;
            }
        }
    }
}
