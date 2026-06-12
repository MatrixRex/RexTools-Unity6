using UnityEngine;
using System.IO;

namespace RexTools.TextureRepacker.Editor
{
    public static class TextureUnpacker
    {
        public static void Unpack(
            Texture2D source,
            bool[] activeChannels,
            string[] suffixes,
            bool[] invert,
            string outputName,
            string outputPath,
            System.Action<string, float> progressCallback = null)
        {
            if (source == null) return;

            Color[] pixels = TextureRepackerUtils.GetReadablePixels(source);
            int w = source.width;
            int h = source.height;

            for (int i = 0; i < 4; i++)
            {
                if (!activeChannels[i]) continue;

                if (progressCallback != null)
                {
                    progressCallback($"Extracting channel {i}...", (float)i / 4f);
                }

                Texture2D res = new Texture2D(w, h, TextureFormat.RGB24, false);
                Color[] resPixels = new Color[pixels.Length];
                bool inv = invert[i];

                for (int p = 0; p < pixels.Length; p++)
                {
                    float val = i == 0 ? pixels[p].r : i == 1 ? pixels[p].g : i == 2 ? pixels[p].b : pixels[p].a;
                    if (inv) val = 1f - val;
                    resPixels[p] = new Color(val, val, val, 1f);
                }
                res.SetPixels(resPixels);
                res.Apply();

                string outFilePath = Path.Combine(outputPath, outputName + suffixes[i] + ".png").Replace('\\', '/');
                File.WriteAllBytes(outFilePath, res.EncodeToPNG());
                Object.DestroyImmediate(res);
            }
        }

        public static Texture2D GenerateChannelPreview(
            Texture2D source,
            int channel,
            bool invert,
            System.Action<string, float> progressCallback = null)
        {
            if (source == null) return null;

            int w = source.width;
            int h = source.height;
            Texture2D result = new Texture2D(w, h, TextureFormat.RGBA32, false);
            Color[] pixels = new Color[w * h];
            Color[] srcPixels = TextureRepackerUtils.GetReadablePixels(source);

            int progressInterval = Mathf.Max(1, h / 20);
            for (int y = 0; y < h; y++)
            {
                if (y % progressInterval == 0 && progressCallback != null)
                {
                    progressCallback($"Processing row {y}/{h}...", (float)y / h);
                }

                for (int x = 0; x < w; x++)
                {
                    Color p = srcPixels[y * w + x];
                    float val = channel == 0 ? p.r : channel == 1 ? p.g : channel == 2 ? p.b : p.a;
                    if (invert) val = 1f - val;
                    pixels[y * w + x] = new Color(val, val, val, 1f);
                }
            }

            result.SetPixels(pixels);
            result.Apply();
            return result;
        }
    }
}
