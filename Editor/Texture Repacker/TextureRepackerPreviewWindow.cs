using UnityEngine;
using UnityEditor;
using UnityEngine.UIElements;

namespace RexTools.TextureRepacker.Editor
{
    public class TextureRepackerPreviewWindow : EditorWindow
    {
        private Image _image;
        private TexturePackSeparator _owner;
        private int _mode;
        private Texture2D _highResTexture;
        private Label _resLabel;

        public static void ShowWindow(TexturePackSeparator owner, int mode, string title)
        {
            var window = GetWindow<TextureRepackerPreviewWindow>("Rex Tools - " + title);
            window._owner = owner;
            window._mode = mode;
            window.minSize = new Vector2(512, 512);
            window.RefreshHighRes();
        }

        private void CreateGUI()
        {
            var toolbar = new VisualElement 
            { 
                style = { flexDirection = FlexDirection.Row, backgroundColor = new Color(0.2f, 0.2f, 0.2f), paddingLeft = 5, paddingRight = 5, height = 25, alignItems = Align.Center } 
            };
            
            var refreshBtn = new Button { text = "Refresh High-Res", style = { height = 20, fontSize = 10 } };
            refreshBtn.clicked += RefreshHighRes;
            toolbar.Add(refreshBtn);

            _resLabel = new Label("Resolution: -") { style = { marginLeft = 10, fontSize = 10, color = Color.gray } };
            toolbar.Add(_resLabel);

            rootVisualElement.Add(toolbar);

            _image = new Image { scaleMode = ScaleMode.ScaleToFit };
            _image.style.flexGrow = 1;
            _image.style.backgroundColor = new Color(0.1f, 0.1f, 0.1f);
            rootVisualElement.Add(_image);
            
            if (_highResTexture != null) RefreshHighRes();
        }

        public void RefreshHighRes()
        {
            if (_owner == null) return;

            if (_highResTexture != null) Object.DestroyImmediate(_highResTexture);
            _highResTexture = _owner.GenerateFullResResult(_mode);

            if (_highResTexture != null)
            {
                if (_image != null) _image.image = _highResTexture;
                if (_resLabel != null) _resLabel.text = $"Resolution: {_highResTexture.width} x {_highResTexture.height}";
            }
        }

        private void OnDestroy()
        {
            if (_highResTexture != null) Object.DestroyImmediate(_highResTexture);
        }
    }
}
