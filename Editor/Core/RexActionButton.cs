using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace RexTools.Editor.Core
{
    public class RexActionButton : VisualElement
    {
        private readonly Label labelElement;
        private readonly Image iconElement;
        private Color tint = new Color(0.2f, 0.5f, 1f);
        private bool isEnabled = true;
        private bool isHovering;
        private bool isPressed;

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

        public Color Tint
        {
            get => tint;
            set
            {
                tint = value;
                UpdateAppearance();
            }
        }

        public bool IsEnabled
        {
            get => isEnabled;
            set
            {
                isEnabled = value;
                UpdateAppearance();
            }
        }

        public event Action OnClick;

        public RexActionButton(string label = null, Texture2D icon = null, Color? tint = null)
        {
            AddToClassList("rex-action-button");

            if (tint.HasValue)
                this.tint = tint.Value;

            bool hasIcon = icon != null;
            bool hasLabel = !string.IsNullOrEmpty(label);

            if (hasIcon)
            {
                iconElement = new Image { image = icon, pickingMode = PickingMode.Ignore };
                iconElement.AddToClassList("rex-action-button__icon");
                hierarchy.Add(iconElement);
            }

            if (hasLabel)
            {
                labelElement = new Label(label) { pickingMode = PickingMode.Ignore };
                labelElement.AddToClassList("rex-action-button__label");
                hierarchy.Add(labelElement);
            }

            RegisterCallback<ClickEvent>(OnClickEvent);
            RegisterCallback<PointerEnterEvent>(OnPointerEnter);
            RegisterCallback<PointerLeaveEvent>(OnPointerLeave);
            RegisterCallback<PointerDownEvent>(OnPointerDown);
            RegisterCallback<PointerUpEvent>(OnPointerUp);
            RegisterCallback<PointerCaptureOutEvent>(OnPointerCaptureOut);

            UpdateAppearance();
        }

        public void SetEnabledWithoutNotify(bool enabled)
        {
            isEnabled = enabled;
            UpdateAppearance();
        }

        private void OnClickEvent(ClickEvent evt)
        {
            evt.StopPropagation();
            if (!isEnabled) return;

            Flash();
            OnClick?.Invoke();
        }

        private void OnPointerEnter(PointerEnterEvent evt)
        {
            isHovering = true;
            UpdateAppearance();
        }

        private void OnPointerLeave(PointerLeaveEvent evt)
        {
            isHovering = false;
            isPressed = false;
            UpdateAppearance();
        }

        private void OnPointerDown(PointerDownEvent evt)
        {
            if (evt.button != 0) return;
            isPressed = true;
            UpdateAppearance();
            evt.StopPropagation();
        }

        private void OnPointerUp(PointerUpEvent evt)
        {
            if (evt.button != 0) return;
            isPressed = false;
            UpdateAppearance();
            evt.StopPropagation();
        }

        private void OnPointerCaptureOut(PointerCaptureOutEvent evt)
        {
            isPressed = false;
            UpdateAppearance();
        }

        private void Flash()
        {
            style.backgroundColor = new StyleColor(Color.Lerp(tint, Color.white, 0.3f));
            schedule.Execute(() => UpdateAppearance()).StartingIn(100);
        }

        private void UpdateAppearance()
        {
            if (!isEnabled)
            {
                style.opacity = 0.4f;
                style.backgroundColor = new StyleColor(tint);
                return;
            }

            style.opacity = 1f;

            Color bg;
            if (isPressed) bg = Darken(tint, 0.15f);
            else if (isHovering) bg = Lighten(tint, 0.08f);
            else bg = tint;

            style.backgroundColor = new StyleColor(bg);
        }

        private static Color Lighten(Color c, float amount)
        {
            return new Color(
                Mathf.Min(c.r + amount, 1f),
                Mathf.Min(c.g + amount, 1f),
                Mathf.Min(c.b + amount, 1f),
                c.a
            );
        }

        private static Color Darken(Color c, float amount)
        {
            return new Color(
                Mathf.Max(c.r - amount, 0f),
                Mathf.Max(c.g - amount, 0f),
                Mathf.Max(c.b - amount, 0f),
                c.a
            );
        }
    }
}
