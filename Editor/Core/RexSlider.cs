using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace RexTools.Editor.Core
{
    public class RexSlider : VisualElement
    {
        private readonly VisualElement trackContainer;
        private readonly VisualElement trackFill;
        private readonly VisualElement trackThumb;
        private readonly VisualElement tickContainer;
        private readonly FloatField valueField;
        private readonly RexButton resetButton;

        private float minValue;
        private float maxValue = 1f;
        private float currentValue;
        private float defaultValue;
        private float snapIncrement;
        private bool isDragging;

        public float MinValue
        {
            get => minValue;
            set
            {
                minValue = value;
                RebuildTicks();
                ClampAndUpdate(currentValue, true);
            }
        }

        public float MaxValue
        {
            get => maxValue;
            set
            {
                maxValue = value;
                RebuildTicks();
                ClampAndUpdate(currentValue, true);
            }
        }

        public float Value
        {
            get => currentValue;
            set => ClampAndUpdate(value, true);
        }

        public float DefaultValue
        {
            get => defaultValue;
            set => defaultValue = value;
        }

        public float SnapIncrement
        {
            get => snapIncrement;
            set
            {
                snapIncrement = Math.Max(value, 0f);
                RebuildTicks();
                if (snapIncrement > 0f)
                    ClampAndUpdate(currentValue, true);
            }
        }

        public event Action<float> OnValueChanged;

        public RexSlider(float min = 0f, float max = 1f,
                         float defaultValue = 0f, float value = 0f,
                         float snapIncrement = 0f)
        {
            AddToClassList("rex-slider");

            this.minValue = min;
            this.maxValue = max;
            this.defaultValue = defaultValue;
            this.snapIncrement = Math.Max(snapIncrement, 0f);

            trackContainer = new VisualElement();
            trackContainer.AddToClassList("rex-slider__track-container");
            hierarchy.Add(trackContainer);

            trackFill = new VisualElement();
            trackFill.AddToClassList("rex-slider__fill");
            trackFill.pickingMode = PickingMode.Ignore;
            trackContainer.Add(trackFill);

            trackThumb = new VisualElement();
            trackThumb.AddToClassList("rex-slider__thumb");
            trackThumb.pickingMode = PickingMode.Ignore;
            trackContainer.Add(trackThumb);

            tickContainer = new VisualElement();
            tickContainer.AddToClassList("rex-slider__ticks");
            tickContainer.pickingMode = PickingMode.Ignore;
            trackContainer.Add(tickContainer);
            RebuildTicks();

            trackContainer.RegisterCallback<PointerDownEvent>(OnPointerDown);
            trackContainer.RegisterCallback<PointerMoveEvent>(OnPointerMove);
            trackContainer.RegisterCallback<PointerUpEvent>(OnPointerUp);
            trackContainer.RegisterCallback<PointerCaptureOutEvent>(OnPointerCaptureOut);

            valueField = new FloatField { isDelayed = true };
            valueField.style.width = 40;
            valueField.style.flexShrink = 0;
            var fieldInput = valueField.Q(className: "unity-float-field__input");
            if (fieldInput != null) fieldInput.style.unityTextAlign = TextAnchor.MiddleRight;
            valueField.RegisterValueChangedCallback(e =>
            {
                float clamped = Mathf.Clamp(e.newValue, minValue, maxValue);
                if (Math.Abs(clamped - e.newValue) > 0.0001f)
                    valueField.SetValueWithoutNotify(clamped);
                ClampAndUpdate(clamped, true);
            });
            hierarchy.Add(valueField);

            var refreshIcon = EditorGUIUtility.IconContent("d_Refresh");
            resetButton = new RexButton(icon: refreshIcon?.image as Texture2D)
            {
                tooltip = $"Reset to {FormatValueText(defaultValue)}"
            };
            resetButton.OnClick += () => ClampAndUpdate(defaultValue, true);
            hierarchy.Add(resetButton);

            currentValue = Mathf.Clamp(value, minValue, maxValue);
            if (snapIncrement > 0f)
                currentValue = SnapValue(currentValue);
            UpdateDisplay(currentValue);
        }

        public void SetValueWithoutNotify(float value)
        {
            ClampAndUpdate(value, false);
        }

        private string FormatValueText(float value)
        {
            if (Math.Abs(value - (int)Math.Round(value)) < 0.0001f)
                return ((int)Math.Round(value)).ToString();
            return value.ToString("F2");
        }

        private void ClampAndUpdate(float value, bool notify)
        {
            float clamped = Mathf.Clamp(value, minValue, maxValue);
            if (snapIncrement > 0f)
                clamped = SnapValue(clamped);

            if (Math.Abs(currentValue - clamped) > 0.0001f)
            {
                currentValue = clamped;
                UpdateDisplay(clamped);
                if (notify)
                    OnValueChanged?.Invoke(clamped);
            }
        }

        private float SnapValue(float value)
        {
            float snapped = Mathf.Round(value / snapIncrement) * snapIncrement;
            return Mathf.Clamp(snapped, minValue, maxValue);
        }

        private void UpdateDisplay(float value)
        {
            float range = maxValue - minValue;
            float ratio = range > 0.0001f ? (value - minValue) / range : 0f;
            trackFill.style.width = Length.Percent(ratio * 100f);
            trackThumb.style.left = Length.Percent(ratio * 100f);
            UpdateValueLabel(value);
        }

        private void UpdateValueLabel(float value)
        {
            valueField.SetValueWithoutNotify(value);
            resetButton.tooltip = $"Reset to {FormatValueText(defaultValue)}";
        }

        private void RebuildTicks()
        {
            tickContainer.Clear();

            if (snapIncrement <= 0f || maxValue <= minValue)
            {
                tickContainer.style.display = DisplayStyle.None;
                return;
            }

            tickContainer.style.display = DisplayStyle.Flex;
            float range = maxValue - minValue;

            for (float v = minValue + snapIncrement; v < maxValue; v += snapIncrement)
            {
                float pct = ((v - minValue) / range) * 100f;
                var tick = new VisualElement();
                tick.AddToClassList("rex-slider__tick");
                tick.style.left = Length.Percent(pct);
                tickContainer.Add(tick);
            }
        }

        private void OnPointerDown(PointerDownEvent evt)
        {
            if (evt.button != 0) return;
            isDragging = true;
            trackContainer.CapturePointer(evt.pointerId);
            UpdateValueFromPointer(evt.localPosition.x);
            evt.StopPropagation();
        }

        private void OnPointerMove(PointerMoveEvent evt)
        {
            if (!isDragging) return;
            UpdateValueFromPointer(evt.localPosition.x);
            evt.StopPropagation();
        }

        private void OnPointerUp(PointerUpEvent evt)
        {
            if (!isDragging || evt.button != 0) return;
            isDragging = false;
            trackContainer.ReleasePointer(evt.pointerId);
            evt.StopPropagation();
        }

        private void OnPointerCaptureOut(PointerCaptureOutEvent evt)
        {
            isDragging = false;
        }

        private void UpdateValueFromPointer(float localX)
        {
            float width = trackContainer.resolvedStyle.width;
            if (width < 0.0001f) return;

            float ratio = Mathf.Clamp01(localX / width);
            float rawValue = minValue + ratio * (maxValue - minValue);
            ClampAndUpdate(rawValue, true);
        }
    }
}
