using System;
using System.Collections.Generic;
using UnityEngine.UIElements;

namespace RexTools.Editor.Core
{
    public class RexTabGroup : VisualElement
    {
        private readonly List<Button> tabButtons = new List<Button>();
        private readonly List<string> tabLabels = new List<string>();
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
            Add(button);
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
