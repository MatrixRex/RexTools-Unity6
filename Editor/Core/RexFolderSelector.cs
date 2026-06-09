using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace RexTools.Editor.Core
{
    public class RexFolderSelector : VisualElement
    {
        private readonly TextField _pathField;
        private readonly Label _dropHint;

        private string _path;

        public string PathValue
        {
            get => _path;
            set
            {
                if (_path != value)
                {
                    _path = value;
                    _pathField.value = value;
                    UpdateHint();
                    OnValueChanged?.Invoke(_path);
                }
            }
        }

        public event Action<string> OnValueChanged;

        public RexFolderSelector()
        {
            AddToClassList("rex-folder-selector");

            var fieldWrapper = new VisualElement();
            fieldWrapper.AddToClassList("rex-folder-selector__field-wrapper");
            hierarchy.Add(fieldWrapper);

            _pathField = new TextField();
            _pathField.AddToClassList("rex-folder-selector__field");
            _pathField.RegisterValueChangedCallback(e =>
            {
                _path = e.newValue;
                UpdateHint();
                OnValueChanged?.Invoke(_path);
            });
            fieldWrapper.Add(_pathField);

            _dropHint = new Label("Drop a folder here");
            _dropHint.AddToClassList("rex-folder-selector__hint");
            fieldWrapper.Add(_dropHint);

            var actions = new VisualElement();
            actions.AddToClassList("rex-folder-selector__actions");
            Add(actions);

            var browseBtn = new Button();
            browseBtn.AddToClassList("rex-icon-button");
            browseBtn.tooltip = "Browse folder";
            var browseIcon = new VisualElement();
            browseIcon.style.backgroundImage = (Texture2D)EditorGUIUtility.IconContent("Folder Icon").image;
            browseIcon.style.width = 14;
            browseIcon.style.height = 14;
            browseBtn.Add(browseIcon);
            browseBtn.clicked += BrowseFolder;
            actions.Add(browseBtn);

            var revealBtn = new Button();
            revealBtn.AddToClassList("rex-icon-button");
            revealBtn.tooltip = "Show in Explorer";
            var revealIcon = new VisualElement();
            revealIcon.style.backgroundImage = (Texture2D)EditorGUIUtility.IconContent("d_Profiler.Open").image;
            revealIcon.style.width = 14;
            revealIcon.style.height = 14;
            revealBtn.Add(revealIcon);
            revealBtn.clicked += RevealInExplorer;
            actions.Add(revealBtn);

            RegisterCallback<DragUpdatedEvent>(OnDragUpdated);
            RegisterCallback<DragPerformEvent>(OnDragPerform);
            RegisterCallback<DragLeaveEvent>(OnDragLeave);

            UpdateHint();
        }

        public void SetPathWithoutNotify(string path)
        {
            _path = path;
            _pathField.SetValueWithoutNotify(path);
            UpdateHint();
        }

        private void BrowseFolder()
        {
            var selected = EditorUtility.OpenFolderPanel("Select Folder", _path ?? "", "");
            if (!string.IsNullOrEmpty(selected))
                PathValue = selected.Replace("\\", "/");
        }

        private void RevealInExplorer()
        {
            if (!string.IsNullOrEmpty(_path) && Directory.Exists(_path))
                EditorUtility.RevealInFinder(_path);
        }

        private void UpdateHint()
        {
            _dropHint.style.display = string.IsNullOrEmpty(_path) ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private void OnDragUpdated(DragUpdatedEvent e)
        {
            if (DragAndDrop.paths.Any(p => Directory.Exists(p)))
            {
                DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
                AddToClassList("rex-folder-selector--dragging");
            }
        }

        private void OnDragPerform(DragPerformEvent e)
        {
            RemoveFromClassList("rex-folder-selector--dragging");
            var folder = DragAndDrop.paths.FirstOrDefault(p => Directory.Exists(p));
            if (folder != null)
                PathValue = folder.Replace("\\", "/");
        }

        private void OnDragLeave(DragLeaveEvent e)
        {
            RemoveFromClassList("rex-folder-selector--dragging");
        }
    }
}
