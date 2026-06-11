using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace RexTools.Editor.Core
{
    /// <summary>
    /// A reusable, styled texture preview component in RexTools, featuring an image container
    /// and a maximize button to view the texture full-size.
    /// </summary>
    public class RexTexturePreview : VisualElement
    {
        private Image previewImage;
        private Button maxBtn;

        public Texture image
        {
            get => previewImage.image;
            set => previewImage.image = value;
        }

        public Action OnMaximizeClicked;

        public RexTexturePreview(float size = 160, string tooltip = "Show full-size preview")
        {
            style.width = size;
            style.height = size;
            style.position = Position.Relative;
            style.flexShrink = 0;

            previewImage = new Image { style = { width = Length.Percent(100), height = Length.Percent(100), backgroundColor = Color.black } };
            previewImage.scaleMode = ScaleMode.ScaleToFit;
            Add(previewImage);

            maxBtn = new Button();
            maxBtn.AddToClassList("rex-maximize-btn");
            maxBtn.tooltip = tooltip;
            maxBtn.clicked += () => OnMaximizeClicked?.Invoke();

            var maxIcon = new Image { image = EditorGUIUtility.IconContent("d_Profiler.Open").image, pickingMode = PickingMode.Ignore };
            maxIcon.style.width = 14;
            maxIcon.style.height = 14;
            maxIcon.tintColor = new Color(0.8f, 0.8f, 0.8f, 1f);
            maxBtn.Add(maxIcon);

            Add(maxBtn);
        }
    }
}
