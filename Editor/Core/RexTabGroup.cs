using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;


namespace RexTools.Editor.Core
{
    public class RexTabGroup : VisualElement
    {
        private readonly List<Button> tabButtons = new List<Button>();
        private readonly List<string> tabLabels = new List<string>();
        private readonly List<VisualElement> tabBadges = new List<VisualElement>();
        private int selectedIndex = -1;

        public event Action<int> OnTabChanged;

        public int SelectedIndex
        {
            get => selectedIndex;
            set => SetSelectedTab(value);
        }

        public RexTabGroup()
        {
            AddToClassList("rex-tabs-container");
        }

        public RexTabGroup(IEnumerable<string> labels) : this()
        {
            foreach (var label in labels)
            {
                AddTab(label);
            }
            if (tabButtons.Count > 0)
            {
                SetSelectedTab(0);
            }
        }

        public void AddTab(string label)
        {
            int index = tabButtons.Count;
            var button = new Button { text = label };
            button.AddToClassList("rex-tab-button");
            button.AddToClassList("rex-tab-button--inactive");
            button.clicked += () => SetSelectedTab(index);

            tabButtons.Add(button);
            tabLabels.Add(label);
            tabBadges.Add(null);
            Add(button);
        }

        public void SetTabBadge(int index, bool show)
        {
            if (index < 0 || index >= tabButtons.Count) return;

            if (tabBadges[index] == null)
            {
                var button = tabButtons[index];
                button.style.position = Position.Relative;

                var badge = new VisualElement();
                badge.AddToClassList("git-badge-dot");
                badge.style.display = DisplayStyle.None;
                
                // Position specifically for tab buttons
                badge.style.position = Position.Absolute;
                badge.style.top = 2;
                badge.style.right = 4;
                badge.style.width = 6;
                badge.style.height = 6;
                badge.style.borderTopLeftRadius = 3;
                badge.style.borderTopRightRadius = 3;
                badge.style.borderBottomLeftRadius = 3;
                badge.style.borderBottomRightRadius = 3;
                badge.style.backgroundColor = new StyleColor(new Color(255f/255f, 89f/255f, 89f/255f));

                button.Add(badge);
                tabBadges[index] = badge;
            }

            tabBadges[index].style.display = show ? DisplayStyle.Flex : DisplayStyle.None;
        }

        public void SetSelectedTab(int index)
        {
            if (index < 0 || index >= tabButtons.Count) return;

            if (selectedIndex != index)
            {
                selectedIndex = index;
                UpdateTabStyles();
                OnTabChanged?.Invoke(selectedIndex);
            }
        }

        public void SetSelectedTabWithoutNotify(int index)
        {
            if (index < 0 || index >= tabButtons.Count) return;

            if (selectedIndex != index)
            {
                selectedIndex = index;
                UpdateTabStyles();
            }
        }

        private void UpdateTabStyles()
        {
            for (int i = 0; i < tabButtons.Count; i++)
            {
                var button = tabButtons[i];
                if (i == selectedIndex)
                {
                    button.RemoveFromClassList("rex-tab-button--inactive");
                    button.AddToClassList("rex-tab-button--active");
                }
                else
                {
                    button.RemoveFromClassList("rex-tab-button--active");
                    button.AddToClassList("rex-tab-button--inactive");
                }
            }
        }
    }
}
