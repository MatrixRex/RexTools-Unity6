using UnityEngine;

namespace RexTools.TextureRepacker.Editor
{
    public enum BlendMode 
    { 
        Multiply, Add, Screen, Overlay, Subtract, Divide, Darken, Lighten, SoftLight, HardLight 
    }

    public static class TextureMixer
    {
        public static Texture2D Mix(
            Texture2D baseTex,
            Texture2D layerTex,
            int baseChannel,
            int layerChannel,
            BlendMode blendMode,
            float opacity,
            System.Action<string, float> progressCallback = null)
        {
            if (baseTex == null) return null;

            int w = baseTex.width;
            int h = baseTex.height;

            Texture2D result = new Texture2D(w, h, TextureFormat.RGBA32, false);
            Color[] pixels = new Color[w * h];

            Color[] basePixels = TextureRepackerUtils.GetReadablePixels(baseTex);
            Color[] layerPixels = layerTex != null ? TextureRepackerUtils.GetReadablePixels(layerTex) : null;
            int lW = layerTex != null ? layerTex.width : 1;
            int lH = layerTex != null ? layerTex.height : 1;

            int progressInterval = Mathf.Max(1, h / 20);
            for (int y = 0; y < h; y++)
            {
                if (y % progressInterval == 0 && progressCallback != null)
                {
                    progressCallback($"Row {y}/{h}", (float)y / h);
                }

                for (int x = 0; x < w; x++)
                {
                    Color bc = basePixels[y * w + x];
                    Color lc = Color.black;
                    if (layerPixels != null)
                    {
                        int lx = Mathf.Clamp(x * lW / w, 0, lW - 1);
                        int ly = Mathf.Clamp(y * lH / h, 0, lH - 1);
                        lc = layerPixels[ly * lW + lx];
                    }
                    Color fb = ApplyChannelSelect(bc, baseChannel);
                    Color fl = ApplyChannelSelect(lc, layerChannel);
                    Color blended = BlendColors(fb, fl, blendMode);
                    pixels[y * w + x] = Color.Lerp(fb, blended, opacity);
                }
            }

            result.SetPixels(pixels);
            result.Apply();
            return result;
        }

        public static Color ApplyChannelSelect(Color c, int channel)
        {
            if (channel < 0) return c; // Full
            float v = channel == 0 ? c.r : channel == 1 ? c.g : channel == 2 ? c.b : c.a;
            return new Color(v, v, v, 1f);
        }

        public static Color BlendColors(Color b, Color l, BlendMode mode)
        {
            Color result = b;
            switch (mode)
            {
                case BlendMode.Multiply:  result = new Color(b.r * l.r, b.g * l.g, b.b * l.b, b.a * l.a); break;
                case BlendMode.Add:       result = new Color(Mathf.Clamp01(b.r + l.r), Mathf.Clamp01(b.g + l.g), Mathf.Clamp01(b.b + l.b), Mathf.Clamp01(b.a + l.a)); break;
                case BlendMode.Screen:    result = new Color(1 - (1 - b.r) * (1 - l.r), 1 - (1 - b.g) * (1 - l.g), 1 - (1 - b.b) * (1 - l.b), 1 - (1 - b.a) * (1 - l.a)); break;
                case BlendMode.Subtract:  result = new Color(Mathf.Clamp01(b.r - l.r), Mathf.Clamp01(b.g - l.g), Mathf.Clamp01(b.b - l.b), Mathf.Clamp01(b.a - l.a)); break;
                case BlendMode.Divide:    result = new Color(Mathf.Clamp01(l.r < 0.001f ? 1f : b.r / l.r), Mathf.Clamp01(l.g < 0.001f ? 1f : b.g / l.g), Mathf.Clamp01(l.b < 0.001f ? 1f : b.b / l.b), Mathf.Clamp01(l.a < 0.001f ? 1f : b.a / l.a)); break;
                case BlendMode.Darken:    result = new Color(Mathf.Min(b.r, l.r), Mathf.Min(b.g, l.g), Mathf.Min(b.b, l.b), Mathf.Min(b.a, l.a)); break;
                case BlendMode.Lighten:   result = new Color(Mathf.Max(b.r, l.r), Mathf.Max(b.g, l.g), Mathf.Max(b.b, l.b), Mathf.Max(b.a, l.a)); break;
                case BlendMode.Overlay:   result = new Color(OverlayChannel(b.r, l.r), OverlayChannel(b.g, l.g), OverlayChannel(b.b, l.b), OverlayChannel(b.a, l.a)); break;
                case BlendMode.SoftLight: result = new Color(SoftLightChannel(b.r, l.r), SoftLightChannel(b.g, l.g), SoftLightChannel(b.b, l.b), SoftLightChannel(b.a, l.a)); break;
                case BlendMode.HardLight: result = new Color(OverlayChannel(l.r, b.r), OverlayChannel(l.g, b.g), OverlayChannel(l.b, b.b), OverlayChannel(l.a, b.a)); break;
            }
            return result;
        }

        private static float OverlayChannel(float b, float l)
            => b < 0.5f ? 2f * b * l : 1f - 2f * (1f - b) * (1f - l);

        private static float SoftLightChannel(float b, float l)
            => l < 0.5f
                ? b - (1f - 2f * l) * b * (1f - b)
                : b + (2f * l - 1f) * (D(b) - b);

        private static float D(float b) => b <= 0.25f ? ((16f * b - 12f) * b + 4f) * b : Mathf.Sqrt(b);
    }
}
