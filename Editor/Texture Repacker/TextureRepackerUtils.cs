using UnityEngine;
using System.Collections.Generic;

namespace RexTools.TextureRepacker.Editor
{
    public static class TextureRepackerUtils
    {
        private static readonly Dictionary<int, Color[]> PixelCache = new Dictionary<int, Color[]>();

        public static void ClearCache()
        {
            PixelCache.Clear();
        }

        public static void RemoveFromCache(int instanceID)
        {
            PixelCache.Remove(instanceID);
        }

        public static Color[] GetReadablePixels(Texture2D tex)
        {
            if (tex == null) return new Color[0];

            int id = tex.GetInstanceID();
            if (PixelCache.TryGetValue(id, out var cached)) return cached;

            Color[] pixels;
            if (tex.isReadable)
            {
                pixels = tex.GetPixels();
            }
            else
            {
                var copy = MakeReadableCopy(tex);
                pixels = copy.GetPixels();
                Object.DestroyImmediate(copy);
            }
            PixelCache[id] = pixels;
            return pixels;
        }

        public static Texture2D MakeReadableCopy(Texture2D tex)
        {
            RenderTexture tmp = RenderTexture.GetTemporary(tex.width, tex.height, 0, RenderTextureFormat.ARGB32);
            Graphics.Blit(tex, tmp);
            RenderTexture prev = RenderTexture.active;
            RenderTexture.active = tmp;
            Texture2D readable = new Texture2D(tex.width, tex.height, TextureFormat.RGBA32, false);
            readable.ReadPixels(new Rect(0, 0, tex.width, tex.height), 0, 0);
            readable.Apply();
            RenderTexture.active = prev;
            RenderTexture.ReleaseTemporary(tmp);
            return readable;
        }

        public static string GenerateBaseName(string name)
        {
            string[] suffixes = { "_packed", "_pack", "_combined", "_tex", "_diffuse", "_albedo" };
            foreach (var s in suffixes)
            {
                if (name.EndsWith(s, System.StringComparison.OrdinalIgnoreCase))
                    name = name.Substring(0, name.Length - s.Length);
            }
            return name.TrimEnd('_', '-', ' ');
        }
    }
}
