using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace RexTools.Editor.Core
{
    public class RexButton : VisualElement
    {
        private readonly Image iconElement;
        private readonly Label labelElement;
        private bool isToggle;
        private bool isActive;

        public string Label
        {
            get => labelElement?.text;
            set
            {
                if (labelElement != null)
                    labelElement.text = value;
            }
        }

        public Texture2D Icon
        {
            get => iconElement?.image as Texture2D;
            set
            {
                if (iconElement != null)
                    iconElement.image = value;
            }
        }

        public bool IsToggle
        {
            get => isToggle;
            set => isToggle = value;
        }

        public bool IsActive
        {
            get => isActive;
            set
            {
                if (isActive != value)
                {
                    isActive = value;
                    UpdateActiveState();
                    OnToggleChanged?.Invoke(isActive);
                }
            }
        }

        public event Action OnClick;
        public event Action<bool> OnToggleChanged;

        public RexButton(string label = null, Texture2D icon = null,
                         bool isToggle = false, bool defaultActive = false)
        {
            AddToClassList("rex-button");
            focusable = true;
            tabIndex = 0;

            this.isToggle = isToggle;

            bool hasIcon = icon != null;
            bool hasLabel = !string.IsNullOrEmpty(label);

            if (hasIcon)
            {
                iconElement = new Image { image = icon, pickingMode = PickingMode.Ignore };
                iconElement.AddToClassList("rex-button__icon");
                hierarchy.Add(iconElement);
            }

            if (hasLabel)
            {
                labelElement = new Label(label) { pickingMode = PickingMode.Ignore };
                labelElement.AddToClassList("rex-button__label");
                hierarchy.Add(labelElement);
            }

            if (hasIcon && !hasLabel)
                AddToClassList("rex-button--icon-only");
            else if (hasIcon && hasLabel)
                AddToClassList("rex-button--with-icon");

            if (isToggle && defaultActive)
            {
                isActive = true;
                AddToClassList("rex-button--active");
            }

            RegisterCallback<ClickEvent>(OnClickEvent);
        }

        private void OnClickEvent(ClickEvent evt)
        {
            evt.StopPropagation();
            Flash();
            if (isToggle)
                IsActive = !IsActive;
            OnClick?.Invoke();
        }

        private void Flash()
        {
            style.backgroundColor = new StyleColor(new Color(0.5f, 0.7f, 1f));
            schedule.Execute(() => style.backgroundColor = StyleKeyword.Null).StartingIn(120);
        }

        private void UpdateActiveState()
        {
            if (isActive)
                AddToClassList("rex-button--active");
            else
                RemoveFromClassList("rex-button--active");
        }

        public void SetActiveWithoutNotify(bool active)
        {
            if (isActive != active)
            {
                isActive = active;
                UpdateActiveState();
            }
        }
    }
}
