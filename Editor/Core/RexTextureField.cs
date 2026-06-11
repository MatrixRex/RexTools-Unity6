using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace RexTools.Editor.Core
{
    /// <summary>
    /// A reusable, styled Drag and Drop field for Texture2D assets in RexTools.
    /// </summary>
    public class RexTextureField : VisualElement
    {
        public Action<Texture2D> OnTextureChanged;
        private Texture2D currentTexture;
        private Image previewImage;
        private Label placeholderLabel;
        private string labelText;

        public Texture2D Value
        {
            get => currentTexture;
            set => SetTexture(value, true);
        }

        public RexTextureField(string label = "Drop Texture", float height = 80)
        {
            labelText = label;
            AddToClassList("rex-drag-drop-field");
            style.height = height;
            style.flexDirection = FlexDirection.Column;
            style.alignItems = Align.Center;
            style.justifyContent = Justify.Center;
            style.minHeight = height;

            previewImage = new Image { scaleMode = ScaleMode.ScaleToFit };
            previewImage.AddToClassList("rex-drag-drop-preview");
            previewImage.style.width = height * 0.7f;
            previewImage.style.height = height * 0.7f;
            previewImage.style.display = DisplayStyle.None;
            Add(previewImage);

            placeholderLabel = new Label(labelText);
            placeholderLabel.AddToClassList("rex-drag-drop-label");
            placeholderLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
            Add(placeholderLabel);

            RegisterCallback<DragUpdatedEvent>(e => {
                DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
                AddToClassList("rex-drag-drop-field--active");
            });
            RegisterCallback<DragLeaveEvent>(e => RemoveFromClassList("rex-drag-drop-field--active"));
            RegisterCallback<DragPerformEvent>(e => {
                RemoveFromClassList("rex-drag-drop-field--active");
                DragAndDrop.AcceptDrag();
                var tex = DragAndDrop.objectReferences.OfType<Texture2D>().FirstOrDefault();
                if (tex != null) SetTexture(tex, true);
            });
            RegisterCallback<MouseDownEvent>(e => {
                if (e.button == 0) EditorGUIUtility.ShowObjectPicker<Texture2D>(currentTexture, false, "", GetHashCode());
            });

            this.schedule.Execute(() => {
                if (Event.current != null && Event.current.type == EventType.ExecuteCommand && Event.current.commandName == "ObjectSelectorUpdated") {
                    if (EditorGUIUtility.GetObjectPickerControlID() == GetHashCode())
                        SetTexture(EditorGUIUtility.GetObjectPickerObject() as Texture2D, true);
                }
            }).Every(50);
        }

        public void SetColor(Color col)
        {
            previewImage.image = null;
            previewImage.style.backgroundColor = col;
            previewImage.style.display = DisplayStyle.Flex;
            placeholderLabel.text = $"Value: {col.r:F2}";
            placeholderLabel.style.color = Color.white;
        }

        public void ClearColor()
        {
            previewImage.style.backgroundColor = Color.clear;
            SetTexture(currentTexture, false);
        }

        private void SetTexture(Texture2D tex, bool notify = true)
        {
            currentTexture = tex;
            previewImage.style.backgroundColor = Color.clear;
            if (tex != null) {
                previewImage.image = tex;
                previewImage.style.display = DisplayStyle.Flex;
                placeholderLabel.text = tex.name;
                placeholderLabel.style.color = Color.white;
            } else {
                previewImage.image = null;
                previewImage.style.display = DisplayStyle.None;
                placeholderLabel.text = labelText;
                placeholderLabel.style.color = new Color(0.5f, 0.5f, 0.5f);
            }
            if (notify) OnTextureChanged?.Invoke(tex);
        }
    }
}
